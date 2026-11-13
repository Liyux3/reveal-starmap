using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using RevealStarMap.Runtime.Cards;
using RevealStarMap.State;
using UnityEngine;

namespace RevealStarMap.Runtime;

// Prints collectible cards of what the player is looking at: the previewed asteroid, every
// asteroid during Canvas preview (a set sheet), or otherwise the active world as explored.
// Terrain comes from the game's own timelapse contract (Timelapser.Render): freeze the screen
// and make the target the active world at the end of one frame, render the world cameras into
// an offscreen texture at the end of the next, restore. The card itself is composed on the CPU.
internal static class MapExport
{
    private const int TerrainLongSide = 2400;
    private static bool capturing, framing;

    internal static bool Capturing => capturing;

    // True only while the camera is held on an export frame; preview zoom limits pause meanwhile.
    internal static bool Framing => framing;

    // Whole pixels per cell keep tiles crisp; the long side lands near TerrainLongSide.
    internal static int PixelsPerCell(int widthCells, int heightCells, int maxTextureSize)
    {
        int longCells = Math.Max(1, Math.Max(widthCells, heightCells));
        int cells = Math.Max(1, Math.Min(16, (int)Math.Round(TerrainLongSide / (double)longCells)));
        while (cells > 1 && longCells * cells > maxTextureSize) cells--;
        return cells;
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
        var jobs = new List<Task<CardCanvas>>();
        var cards = new List<CardData>();
        bool restored = false;
        void Restore()
        {
            if (restored) return;
            restored = true;
            framing = false;
            if (timelapse) DebugHandler.SetTimelapseMode(false);
            camera.SnapTo(position, zoom);
            if (OverlayScreen.Instance != null && OverlayScreen.Instance.mode != overlay) OverlayScreen.Instance.ToggleOverlay(overlay, false);
            if (freeze != null) freeze.enabled = false;
            if (frozen != null) UnityEngine.Object.Destroy(frozen);
        }
        CardArt.BindMainThread();
        framing = true;
        bool set = RevealSessionCoordinator.IsCanvasPreviewActive;
        try
        {
            bool preview = set || RevealSessionCoordinator.ActiveMode == RevealPreviewMode.World;
            List<CardData> cluster = CardData.ReadCluster(preview);
            if (set) cards = cluster;
            else
            {
                int id = RevealSessionCoordinator.ActiveMode == RevealPreviewMode.World ? RevealSessionCoordinator.ActiveWorldId : ClusterManager.Instance.activeWorldId;
                WorldContainer? world = ClusterManager.Instance.GetWorld(id);
                if (world == null) yield break;
                if (!preview && !AnyExplored(world.WorldOffset, world.WorldSize))
                {
                    RevealSessionCoordinator.Warn("export.empty");
                    yield break;
                }
                // Rocket interiors are not part of the set; they print as an unnumbered card.
                cards.Add(cluster.FirstOrDefault(c => c.World.id == id) ?? CardData.Read(world, 0, cluster.Count, preview));
            }

            yield return SequenceUtil.WaitForEndOfFrame;
            // Hold the current picture on screen and hide UI for the whole run.
            if (freeze != null)
            {
                frozen = new Texture2D(Camera.main.pixelWidth, Camera.main.pixelHeight, TextureFormat.RGB24, false);
                frozen.ReadPixels(new Rect(0f, 0f, Camera.main.pixelWidth, Camera.main.pixelHeight), 0, 0);
                frozen.Apply();
                freeze.GetComponent<FillRenderTargetEffect>()?.SetFillTexture(frozen);
                freeze.enabled = true;
            }
            OverlayScreen.Instance?.ToggleOverlay(OverlayModes.None.ID, false);
            foreach (CardData card in cards)
            {
                WorldContainer world = card.World;
                Vector3 center = new Vector3(world.WorldOffset.x + world.WorldSize.x / 2f, world.WorldOffset.y + world.WorldSize.y / 2f, position.z);
                // The override remembers the world it replaces, so leave it before entering the next one.
                if (timelapse) DebugHandler.SetTimelapseMode(false);
                DebugHandler.SetTimelapseMode(true, world.id);
                timelapse = true;
                camera.SnapTo(center, GridRevealRuntime.ComputeOrthoSize(world.WorldSize.x, world.WorldSize.y) + 4f);
                CardArt.Pump();
                yield return SequenceUtil.WaitForEndOfFrame;
                CardCanvas? terrain = RenderTerrain(camera, world, center);
                // The card's pixels are pure computation; compose it off the main thread.
                jobs.Add(Task.Run(() => CardComposer.Compose(card, terrain)));
            }
            Restore();
            long captured = timer.ElapsedMilliseconds;
            while (jobs.Any(j => !j.IsCompleted))
            {
                CardArt.Pump();
                yield return null;
            }
            Task<CardCanvas>? failed = jobs.FirstOrDefault(j => j.IsFaulted);
            if (failed != null)
            {
                Exception e = failed.Exception?.GetBaseException() ?? new Exception("unknown");
                UnityEngine.Debug.LogError($"{RevealStarMapMod.Tag} Card composition failed. {e}");
                RevealSessionCoordinator.Warn("error.export", e.Message);
                yield break;
            }
            List<CardCanvas> printed = jobs.Select(j => j.Result).ToList();
            UnityEngine.Debug.Log($"{RevealStarMapMod.Tag} Captured terrain in {captured} ms; cards composed in {timer.ElapsedMilliseconds - captured} ms.");
            // Sheet layout and PNG encoding also run off the main thread; only type is served here.
            Task<byte[]> encode = Task.Run(() => Encode(set ? Sheet(cards, printed) : printed[0]));
            while (!encode.IsCompleted)
            {
                CardArt.Pump();
                yield return null;
            }
            if (encode.IsFaulted)
            {
                Exception e = encode.Exception?.GetBaseException() ?? new Exception("unknown");
                UnityEngine.Debug.LogError($"{RevealStarMapMod.Tag} Map export failed. {e}");
                RevealSessionCoordinator.Warn("error.export", e.Message);
                yield break;
            }
            Save(encode.Result, cards, set, timer);
        }
        finally
        {
            Restore();
            CardArt.Clear();
            capturing = false;
        }
    }

    private static CardCanvas? RenderTerrain(CameraController camera, WorldContainer world, Vector3 center)
    {
        RenderTexture? map = null;
        RenderTexture active = RenderTexture.active;
        try
        {
            int ppc = PixelsPerCell(world.WorldSize.x, world.WorldSize.y, Math.Min(SystemInfo.maxTextureSize, 16384));
            map = new RenderTexture(world.WorldSize.x * ppc, world.WorldSize.y * ppc, 24, RenderTextureFormat.ARGB32);
            camera.SetPosition(center);
            camera.OrthographicSize = world.WorldSize.y / 2f;
            camera.RenderForTimelapser(ref map);
            var image = new Texture2D(map.width, map.height, TextureFormat.RGBA32, false);
            RenderTexture.active = map;
            image.ReadPixels(new Rect(0f, 0f, map.width, map.height), 0, 0);
            CardCanvas terrain = CardCanvas.FromTexture(image);
            UnityEngine.Object.Destroy(image);
            for (int i = 0; i < terrain.P.Length; i++) terrain.P[i].a = 255;
            return terrain;
        }
        catch (Exception exception)
        {
            UnityEngine.Debug.LogError($"{RevealStarMapMod.Tag} Terrain capture failed for {world.GetProperName()}. {exception}");
            return null;
        }
        finally
        {
            RenderTexture.active = active;
            map?.Release();
        }
    }

    // The whole cluster as one sheet: the colony's name in the game's header face over the cards.
    private static CardCanvas Sheet(List<CardData> cards, List<CardCanvas> printed)
    {
        const int cols = 4, gap = 30, margin = 80, head = 240;
        int rows = (printed.Count + cols - 1) / cols;
        int w = 2 * margin + cols * CardComposer.CW + (cols - 1) * gap, h = head + rows * (CardComposer.CH + gap) + margin - gap;
        var sheet = new CardCanvas(w, h, CardCanvas.Rgba(24, 22, 28));
        CardData first = cards[0];
        CardCanvas title = CardArt.Rim(CardArt.Text(first.Colony.ToUpperInvariant(), first.Colony.All(ch => ch < 128)
            ? CardArt.Font("GRAYSTROKE REGULAR SDF") : Localization.FontAsset, 72), 3, CardCanvas.Rgba(23, 22, 28));
        sheet.Composite(title, margin, 60);
        CardCanvas sub = CardArt.Tint(CardArt.Text($"{RevealText.Format("card.set", cards.Count)}  ·  {RevealText.Format("export.cycle", first.Cycle)}  ·  {first.Seed}",
            Localization.FontAsset, 26), CardCanvas.Rgba(170, 166, 156));
        sheet.Composite(sub, margin + 4, 150);
        for (int i = 0; i < printed.Count; i++)
            sheet.Composite(printed[i], margin + i % cols * (CardComposer.CW + gap), head + i / cols * (CardComposer.CH + gap));
        return sheet;
    }

    private static byte[] Encode(CardCanvas image)
    {
        var raw = new byte[image.W * image.H * 4];
        for (int y = 0; y < image.H; y++)
            for (int x = 0; x < image.W; x++)
            {
                Color32 c = image.P[y * image.W + x];
                int o = ((image.H - 1 - y) * image.W + x) * 4;
                raw[o] = c.r; raw[o + 1] = c.g; raw[o + 2] = c.b; raw[o + 3] = c.a;
            }
        return ImageConversion.EncodeArrayToPNG(raw, UnityEngine.Experimental.Rendering.GraphicsFormat.R8G8B8A8_UNorm, (uint)image.W, (uint)image.H);
    }

    private static void Save(byte[] png, List<CardData> cards, bool set, Stopwatch timer)
    {
        try
        {
            string folder = Path.Combine(Util.RootFolder(), "RevealStarMap", "exports");
            Directory.CreateDirectory(folder);
            CardData first = cards[0];
            string subject = set ? RevealText.Get("export.canvas") : first.Name;
            string path = Path.Combine(folder, FileName(first.Colony, subject, first.Cycle, name => File.Exists(Path.Combine(folder, name))));
            File.WriteAllBytes(path, png);
            UnityEngine.Debug.Log($"{RevealStarMapMod.Tag} Exported {cards.Count} card(s) in {timer.ElapsedMilliseconds} ms: {path}");
            // The file is the result of an explicit action, so its location is always shown.
            RevealSessionCoordinator.Warn("status.export.done", path);
        }
        catch (Exception exception)
        {
            UnityEngine.Debug.LogError($"{RevealStarMapMod.Tag} Map export failed. {exception}");
            RevealSessionCoordinator.Warn("error.export", exception.Message);
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
}
