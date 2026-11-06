using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Linq;
using TMPro;
using RevealStarMap.State;
using UnityEngine;

namespace RevealStarMap.Runtime;

// Saves what the player is looking at as a framed PNG: the previewed asteroid, the whole canvas
// during Canvas preview, or otherwise the active world as explored. The capture follows the game's
// own timelapse contract (Timelapser.Render): freeze the screen and frame the target at the end of
// one frame, render the world cameras into an offscreen texture at the end of the next, restore.
internal static class MapExport
{
    internal readonly struct Layout
    {
        internal Layout(int cells, int mapWidth, int mapHeight, int margin, int caption)
        {
            PixelsPerCell = cells;
            MapWidth = mapWidth;
            MapHeight = mapHeight;
            Margin = margin;
            Caption = caption;
        }

        internal int PixelsPerCell { get; }
        internal int MapWidth { get; }
        internal int MapHeight { get; }
        internal int Margin { get; }
        internal int Caption { get; }
        internal int Width => MapWidth + 2 * Margin;
        internal int Height => Margin + MapHeight + Caption;
    }

    private const int TargetLongSide = 3072;
    private const int CaptionLayer = 31;
    private static readonly Color Ground = new Color32(22, 24, 31, 255);
    private static readonly Color TitleColor = new Color32(236, 237, 241, 255);
    private static readonly Color DetailColor = new Color32(150, 155, 168, 255);
    private static readonly Color CreditColor = new Color32(104, 109, 122, 255);
    private static bool capturing;

    internal static bool Capturing => capturing;

    // Integer pixels per cell keep tiles crisp; the long side lands near TargetLongSide.
    internal static Layout ComputeLayout(int widthCells, int heightCells, int maxTextureSize)
    {
        int longCells = Math.Max(1, Math.Max(widthCells, heightCells));
        int cells = Math.Max(1, Math.Min(16, (int)Math.Round(TargetLongSide / (double)longCells)));
        int margin = 0, caption = 0;
        for (; cells >= 1; cells--)
        {
            int longPixels = longCells * cells;
            margin = (int)Math.Round(Math.Min(160.0, Math.Max(48.0, longPixels * 0.035)));
            caption = margin * 3;
            if (widthCells * cells + 2 * margin <= maxTextureSize && heightCells * cells + margin + caption <= maxTextureSize)
                break;
        }
        cells = Math.Max(1, cells);
        return new Layout(cells, widthCells * cells, heightCells * cells, margin, caption);
    }

    internal static string FileName(string colony, string subject, int cycle, Func<string, bool> exists)
    {
        char[] invalid = Path.GetInvalidFileNameChars().Concat(new[] { '/', '\\', ':', '*', '?', '"', '<', '>', '|' }).ToArray();
        string Clean(string text) => string.Join(" ", new string(text.Select(c => invalid.Contains(c) || char.IsControl(c) ? ' ' : c).ToArray())
            .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)).Trim('.', ' ');
        string stem = string.Join(" - ", new[] { Clean(colony), Clean(subject), $"Cycle {cycle}" }.Where(s => s.Length > 0));
        string name = stem + ".png";
        for (int copy = 2; exists(name); copy++) name = $"{stem} ({copy}).png";
        return name;
    }

    internal static void Trigger(MonoBehaviour host)
    {
        // Same readiness as the shortcuts: no capture during a world switch fade or another timelapse.
        if (capturing || DebugHandler.TimelapseMode || CameraController.Instance == null ||
            CameraController.Instance.activeFadeRoutine != null || ClusterManager.Instance == null) return;
        host.StartCoroutine(Capture());
    }

    private static IEnumerator Capture()
    {
        capturing = true;
        CameraController camera = CameraController.Instance;
        Vector3 position = camera.transform.GetPosition();
        float zoom = camera.OrthographicSize;
        HashedString overlay = OverlayScreen.Instance != null ? OverlayScreen.Instance.mode : OverlayModes.None.ID;
        Camera? freeze = camera.timelapseFreezeCamera;
        Texture2D? frozen = null;
        bool timelapse = false;
        var timer = Stopwatch.StartNew();
        try
        {
            bool canvas = RevealSessionCoordinator.IsCanvasPreviewActive;
            WorldContainer? world = ClusterManager.Instance.GetWorld(
                RevealSessionCoordinator.ActiveMode == RevealPreviewMode.World ? RevealSessionCoordinator.ActiveWorldId : ClusterManager.Instance.activeWorldId);
            if (world == null) yield break;
            Vector2I origin = canvas ? new Vector2I(0, 0) : world.WorldOffset;
            Vector2I size = canvas ? new Vector2I(Grid.WidthInCells, Grid.HeightInCells) : world.WorldSize;
            if (!canvas && RevealSessionCoordinator.ActiveMode != RevealPreviewMode.World && !AnyExplored(origin, size))
            {
                RevealSessionCoordinator.Warn("export.empty");
                yield break;
            }
            Layout layout = ComputeLayout(size.x, size.y, Math.Min(SystemInfo.maxTextureSize, 16384));
            Vector3 center = new Vector3(origin.x + size.x / 2f, origin.y + size.y / 2f, position.z);

            yield return SequenceUtil.WaitForEndOfFrame;
            // Frame 1: hold the current picture on screen, hide UI and frame the target. The frame
            // covers the target at the screen's aspect so tile meshes build for all of it.
            if (freeze != null)
            {
                frozen = new Texture2D(Camera.main.pixelWidth, Camera.main.pixelHeight, TextureFormat.RGB24, false);
                frozen.ReadPixels(new Rect(0f, 0f, Camera.main.pixelWidth, Camera.main.pixelHeight), 0, 0);
                frozen.Apply();
                freeze.GetComponent<FillRenderTargetEffect>()?.SetFillTexture(frozen);
                freeze.enabled = true;
            }
            DebugHandler.SetTimelapseMode(true, world.id);
            timelapse = true;
            OverlayScreen.Instance?.ToggleOverlay(OverlayModes.None.ID, false);
            camera.SnapTo(center, GridRevealRuntime.ComputeOrthoSize(size.x, size.y) + 4f);

            yield return SequenceUtil.WaitForEndOfFrame;
            Finish(camera, center, size, layout, canvas, world, timer);
        }
        finally
        {
            if (timelapse) DebugHandler.SetTimelapseMode(false);
            camera.SnapTo(position, zoom);
            if (OverlayScreen.Instance != null && OverlayScreen.Instance.mode != overlay) OverlayScreen.Instance.ToggleOverlay(overlay, false);
            if (freeze != null) freeze.enabled = false;
            if (frozen != null) UnityEngine.Object.Destroy(frozen);
            capturing = false;
        }
    }

    // Frame 2: render exactly the target, then the caption, and read both into one image.
    private static void Finish(CameraController camera, Vector3 center, Vector2I size, Layout layout, bool canvas, WorldContainer world, Stopwatch timer)
    {
        RenderTexture? map = null, caption = null;
        RenderTexture active = RenderTexture.active;
        try
        {
            map = new RenderTexture(layout.MapWidth, layout.MapHeight, 24, RenderTextureFormat.ARGB32);
            camera.SetPosition(center);
            camera.OrthographicSize = size.y / 2f;
            camera.RenderForTimelapser(ref map);
            caption = RenderCaption(layout, CaptionText(canvas, world));
            var image = new Texture2D(layout.Width, layout.Height, TextureFormat.RGB24, false);
            RenderTexture.active = caption;
            image.ReadPixels(new Rect(0f, 0f, layout.Width, layout.Height), 0, 0);
            RenderTexture.active = map;
            image.ReadPixels(new Rect(0f, 0f, layout.MapWidth, layout.MapHeight), layout.Margin, layout.Caption);
            RenderTexture.active = active;
            byte[] png = ImageConversion.EncodeToPNG(image);
            UnityEngine.Object.Destroy(image);

            string folder = Path.Combine(Util.RootFolder(), "RevealStarMap", "exports");
            Directory.CreateDirectory(folder);
            string subject = canvas ? RevealText.Get("export.canvas") : GridRevealRuntime.GetWorldName(world);
            string path = Path.Combine(folder, FileName(SaveGame.Instance?.BaseName ?? "Colony", subject, GameClock.Instance.GetCycle() + 1,
                name => File.Exists(Path.Combine(folder, name))));
            File.WriteAllBytes(path, png);
            UnityEngine.Debug.Log($"{RevealStarMapMod.Tag} Exported {layout.Width}x{layout.Height} at {layout.PixelsPerCell} px per cell in {timer.ElapsedMilliseconds} ms: {path}");
            // The file is the result of an explicit action, so its location is always shown.
            RevealSessionCoordinator.Warn("status.export.done", path);
        }
        catch (Exception exception)
        {
            UnityEngine.Debug.LogError($"{RevealStarMapMod.Tag} Map export failed. {exception}");
            RevealSessionCoordinator.Warn("error.export", exception.Message);
        }
        finally
        {
            RenderTexture.active = active;
            map?.Release();
            caption?.Release();
        }
    }

    private static bool AnyExplored(Vector2I origin, Vector2I size)
    {
        for (int y = origin.y; y < origin.y + size.y; y++)
            for (int x = origin.x; x < origin.x + size.x; x++)
            {
                int cell = Grid.XYToCell(x, y);
                if (Grid.IsValidCell(cell) && Grid.Visible[cell] > 0) return true;
            }
        return false;
    }

    private static (string Title, string Detail) CaptionText(bool canvas, WorldContainer world)
    {
        string colony = SaveGame.Instance?.BaseName ?? string.Empty;
        string cycle = RevealText.Format("export.cycle", GameClock.Instance.GetCycle() + 1);
        string seed = CustomGameSettings.Instance != null ? CustomGameSettings.Instance.GetSettingsCoordinate() : string.Empty;
        string title = canvas ? colony : GridRevealRuntime.GetWorldName(world);
        string[] detail = canvas ? new[] { RevealText.Get("export.canvas"), cycle, seed } : new[] { colony, cycle, seed };
        return (title, string.Join("   ·   ", detail.Where(s => !string.IsNullOrEmpty(s))));
    }

    // Draws the ground and caption with the game's font through a temporary camera, one world unit
    // per pixel, far from the scene and on a layer nothing else uses.
    private static RenderTexture RenderCaption(Layout layout, (string Title, string Detail) text)
    {
        var target = new RenderTexture(layout.Width, layout.Height, 24, RenderTextureFormat.ARGB32);
        var root = new GameObject("RevealStarMapExportCaption");
        try
        {
            root.transform.position = new Vector3(-100000f, -100000f, 0f);
            var lens = new GameObject("Camera").AddComponent<Camera>();
            lens.transform.SetParent(root.transform, false);
            lens.transform.localPosition = new Vector3(layout.Width / 2f, layout.Height / 2f, -10f);
            lens.orthographic = true;
            lens.orthographicSize = layout.Height / 2f;
            lens.clearFlags = CameraClearFlags.SolidColor;
            lens.backgroundColor = Ground;
            lens.cullingMask = 1 << CaptionLayer;
            lens.nearClipPlane = 1f;
            lens.farClipPlane = 20f;
            lens.enabled = false;
            lens.targetTexture = target;

            float unit = layout.Margin;
            float baseline = unit * 0.95f;
            // The credit shares the title's row, so the detail line keeps the full width on narrow maps.
            float row = baseline + unit * 0.62f, inner = layout.Width - 2f * layout.Margin;
            Label(root, text.Title, unit * 0.62f, TitleColor, TextAlignmentOptions.BottomLeft,
                new Vector2(layout.Margin, row), inner * 0.68f);
            Label(root, RevealStarMapMod.ModName, unit * 0.3f, CreditColor, TextAlignmentOptions.BottomRight,
                new Vector2(layout.Width - layout.Margin, row), inner * 0.3f);
            Label(root, text.Detail, unit * 0.34f, DetailColor, TextAlignmentOptions.BottomLeft,
                new Vector2(layout.Margin, baseline), inner);
            lens.Render();
            lens.targetTexture = null;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
        return target;
    }

    private static void Label(GameObject root, string text, float pixels, Color color, TextAlignmentOptions alignment, Vector2 anchor, float width)
    {
        var item = new GameObject("Label") { layer = CaptionLayer };
        item.transform.SetParent(root.transform, false);
        var label = item.AddComponent<TextMeshPro>();
        label.font = Localization.FontAsset;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.alignment = alignment;
        label.color = color;
        label.text = text;
        // Size by measured line height so the result does not depend on TextMeshPro's unit scale.
        label.fontSize = 10f;
        float line = label.GetPreferredValues("Hg").y;
        label.fontSize = line > 0f ? 10f * pixels / line : pixels;
        bool right = alignment == TextAlignmentOptions.BottomRight;
        label.rectTransform.pivot = new Vector2(right ? 1f : 0f, 0f);
        label.rectTransform.sizeDelta = new Vector2(width, pixels * 1.6f);
        label.rectTransform.localPosition = new Vector3(anchor.x, anchor.y - pixels * 0.25f, 0f);
        label.ForceMeshUpdate();
    }
}
