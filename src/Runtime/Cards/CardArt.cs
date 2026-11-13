using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;

namespace RevealStarMap.Runtime.Cards;

// The game's own pixels for the card: UI sprites, starmap art and TextMeshPro type, each read
// back into a CardCanvas through a render texture, so atlas or readability settings never matter.
internal static class CardArt
{
    private const int TextLayer = 31;
    private static readonly Dictionary<string, CardCanvas?> Sprites = new Dictionary<string, CardCanvas?>();
    private static readonly ConcurrentQueue<(Func<object?> Work, TaskCompletionSource<object?> Done)> Queue =
        new ConcurrentQueue<(Func<object?>, TaskCompletionSource<object?>)>();
    private static int mainThread = -1;

    internal static void Clear() => Sprites.Clear();

    // Cards compose on worker threads; anything that touches Unity runs on the main thread,
    // queued here and served once per frame by Pump.
    internal static void BindMainThread() => mainThread = Thread.CurrentThread.ManagedThreadId;

    private static T OnMain<T>(Func<T> work)
    {
        if (Thread.CurrentThread.ManagedThreadId == mainThread) return work();
        var done = new TaskCompletionSource<object?>();
        Queue.Enqueue((() => work(), done));
        return (T)done.Task.Result!;
    }

    internal static void Pump()
    {
        while (Queue.TryDequeue(out var item))
        {
            try { item.Done.SetResult(item.Work()); }
            catch (Exception exception) { item.Done.SetException(exception); }
        }
    }

    internal static TMP_FontAsset Font(string name) => OnMain(() =>
        Resources.FindObjectsOfTypeAll<TMP_FontAsset>().FirstOrDefault(f => f.name == name) ?? Localization.FontAsset);

    internal static CardCanvas? Sprite(Sprite? sprite) => sprite == null ? null : OnMain(() => SpriteNow(sprite));

    internal static CardCanvas? WorldSprite(WorldContainer world) => OnMain(() => SpriteNow(world.GetComponent<ClusterGridEntity>()?.GetUISprite()));

    private static CardCanvas? SpriteNow(Sprite? sprite)
    {
        if (sprite == null) return null;
        Texture texture = sprite.texture;
        Rect rect = sprite.textureRect;
        var rt = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.ARGB32);
        RenderTexture previous = RenderTexture.active;
        Graphics.Blit(texture, rt);
        RenderTexture.active = rt;
        var image = new Texture2D((int)rect.width, (int)rect.height, TextureFormat.RGBA32, false);
        image.ReadPixels(rect, 0, 0);
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(rt);
        CardCanvas canvas = CardCanvas.FromTexture(image);
        UnityEngine.Object.Destroy(image);
        return canvas;
    }

    internal static CardCanvas? Prefab(string id) => OnMain(() =>
    {
        if (Sprites.TryGetValue(id, out CardCanvas? cached)) return cached;
        GameObject? prefab = Assets.TryGetPrefab(id);
        CardCanvas? art = prefab != null ? SpriteNow(Def.GetUISprite(prefab).first) : null;
        Sprites[id] = art;
        return art;
    });

    internal static CardCanvas? Fit(CardCanvas? art, int size)
    {
        if (art == null) return null;
        float s = size / (float)Mathf.Max(art.W, art.H);
        return art.Scaled(Mathf.Max(1, Mathf.RoundToInt(art.W * s)), Mathf.Max(1, Mathf.RoundToInt(art.H * s)));
    }

    // White text cropped to its ink, line height in pixels; tint and rim are applied by the caller.
    internal static CardCanvas Text(string text, TMP_FontAsset font, float pixels) => OnMain(() => TextNow(text, font, pixels));

    private static CardCanvas TextNow(string text, TMP_FontAsset font, float pixels)
    {
        var root = new GameObject("RevealStarMapCardText");
        root.transform.position = new Vector3(-300000f, -300000f, 0f);
        try
        {
            var label = new GameObject("Label") { layer = TextLayer }.AddComponent<TextMeshPro>();
            label.transform.SetParent(root.transform, false);
            label.font = font;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.fontSize = 10f;
            label.text = text;
            float line = label.GetPreferredValues("Hg").y;
            label.fontSize = line > 0f ? 10f * pixels / line : pixels;
            Vector2 size = label.GetPreferredValues(text);
            int w = Mathf.CeilToInt(size.x + 16), h = Mathf.CeilToInt(size.y + 16);
            label.rectTransform.sizeDelta = new Vector2(w, h);
            label.rectTransform.localPosition = Vector3.zero;
            label.ForceMeshUpdate();
            var lens = new GameObject("Camera").AddComponent<Camera>();
            lens.transform.SetParent(root.transform, false);
            lens.transform.localPosition = new Vector3(0f, 0f, -10f);
            lens.orthographic = true;
            lens.orthographicSize = h / 2f;
            lens.aspect = w / (float)h;
            lens.clearFlags = CameraClearFlags.SolidColor;
            lens.backgroundColor = new Color(1f, 1f, 1f, 0f);
            lens.cullingMask = 1 << TextLayer;
            lens.nearClipPlane = 1f;
            lens.farClipPlane = 20f;
            lens.enabled = false;
            var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32);
            lens.targetTexture = rt;
            lens.Render();
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;
            var image = new Texture2D(w, h, TextureFormat.RGBA32, false);
            image.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            RenderTexture.active = previous;
            lens.targetTexture = null;
            RenderTexture.ReleaseTemporary(rt);
            CardCanvas canvas = CardCanvas.FromTexture(image);
            UnityEngine.Object.Destroy(image);
            return Crop(canvas);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static CardCanvas Crop(CardCanvas c)
    {
        int x0 = c.W, y0 = c.H, x1 = -1, y1 = -1;
        for (int y = 0; y < c.H; y++)
            for (int x = 0; x < c.W; x++)
                if (c.P[y * c.W + x].a > 8) { x0 = Mathf.Min(x0, x); y0 = Mathf.Min(y0, y); x1 = Mathf.Max(x1, x); y1 = Mathf.Max(y1, y); }
        if (x1 < 0) return new CardCanvas(1, 1);
        var o = new CardCanvas(x1 - x0 + 1, y1 - y0 + 1);
        for (int y = 0; y < o.H; y++) System.Array.Copy(c.P, (y0 + y) * c.W + x0, o.P, y * o.W, o.W);
        return o;
    }

    // Recolour an alpha shape, optionally with a vertical gradient (gold foil type).
    internal static CardCanvas Tint(CardCanvas c, Color32 top, Color32? bottom = null)
    {
        var o = new CardCanvas(c.W, c.H);
        for (int y = 0; y < c.H; y++)
        {
            Color32 col = bottom.HasValue ? CardCanvas.Mix(top, bottom.Value, y / (float)Mathf.Max(1, c.H - 1)) : top;
            for (int x = 0; x < c.W; x++)
            {
                byte a = c.P[y * c.W + x].a;
                o.P[y * c.W + x] = new Color32(col.r, col.g, col.b, (byte)(a * col.a / 255));
            }
        }
        return o;
    }

    // Dark ink rim behind type, the way the game sets titles on art.
    internal static CardCanvas Rim(CardCanvas c, int px, Color32 ink)
    {
        var o = new CardCanvas(c.W + 2 * px, c.H + 2 * px);
        for (int y = 0; y < c.H; y++)
            for (int x = 0; x < c.W; x++)
            {
                byte a = c.P[y * c.W + x].a;
                if (a < 40) continue;
                for (int dy = -px; dy <= px; dy++)
                    for (int dx = -px; dx <= px; dx++)
                        if (dx * dx + dy * dy <= px * px)
                        {
                            int i = (y + px + dy) * o.W + x + px + dx;
                            if (o.P[i].a < a) o.P[i] = new Color32(ink.r, ink.g, ink.b, a);
                        }
            }
        o.Composite(c, px, px);
        return o;
    }
}
