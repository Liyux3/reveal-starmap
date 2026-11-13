using System;
using System.Collections.Generic;
using UnityEngine;

namespace RevealStarMap.Runtime.Cards;

// A small CPU raster for card chrome: straight-alpha RGBA, origin top-left, so the drawing code
// reads like the reference design (design/cards/mock6.py). GPU work stays with the game's own
// renderers (terrain, text, sprites); their pixels are composited here.
internal sealed class CardCanvas
{
    internal readonly int W, H;
    internal readonly Color32[] P;

    internal CardCanvas(int w, int h, Color32 fill = default)
    {
        W = w; H = h; P = new Color32[w * h];
        if (fill.a != 0) for (int i = 0; i < P.Length; i++) P[i] = fill;
    }

    internal static Color32 Rgba(int r, int g, int b, int a = 255) => new Color32((byte)r, (byte)g, (byte)b, (byte)a);

    internal static Color32 Mix(Color32 a, Color32 b, float t) => new Color32(
        (byte)(a.r + (b.r - a.r) * t), (byte)(a.g + (b.g - a.g) * t), (byte)(a.b + (b.b - a.b) * t), 255);

    // Source-over with straight alpha; coverage scales the source alpha.
    internal void Blend(int x, int y, Color32 c, float coverage = 1f)
    {
        if ((uint)x >= (uint)W || (uint)y >= (uint)H) return;
        float sa = c.a / 255f * coverage;
        if (sa <= 0f) return;
        int i = y * W + x;
        Color32 d = P[i];
        float da = d.a / 255f, oa = sa + da * (1f - sa);
        if (oa <= 0f) return;
        float k = da * (1f - sa);
        P[i] = new Color32((byte)((c.r * sa + d.r * k) / oa), (byte)((c.g * sa + d.g * k) / oa),
            (byte)((c.b * sa + d.b * k) / oa), (byte)(oa * 255f));
    }

    internal void Composite(CardCanvas src, int ox, int oy, byte[]? mask = null, float alpha = 1f)
    {
        for (int y = 0; y < src.H; y++)
        {
            int ty = oy + y;
            if ((uint)ty >= (uint)H) continue;
            for (int x = 0; x < src.W; x++)
            {
                int tx = ox + x;
                if ((uint)tx >= (uint)W) continue;
                float m = mask == null ? 1f : mask[ty * W + tx] / 255f;
                Color32 c = src.P[y * src.W + x];
                if (m >= 1f && alpha >= 1f && c.a == 255) P[ty * W + tx] = c;
                else if (m > 0f) Blend(tx, ty, c, m * alpha);
            }
        }
    }

    // Scanline coverage of a closed polygon, two samples per row.
    internal byte[] PolygonMask(IList<Vector2> pts)
    {
        var mask = new byte[W * H];
        var xs = new List<float>();
        for (int y = 0; y < H; y++)
            for (int s = 0; s < 2; s++)
            {
                float sy = y + 0.25f + 0.5f * s;
                xs.Clear();
                for (int i = 0, j = pts.Count - 1; i < pts.Count; j = i++)
                {
                    Vector2 a = pts[i], b = pts[j];
                    if ((a.y <= sy && b.y > sy) || (b.y <= sy && a.y > sy))
                        xs.Add(a.x + (sy - a.y) / (b.y - a.y) * (b.x - a.x));
                }
                xs.Sort();
                for (int k = 0; k + 1 < xs.Count; k += 2)
                {
                    int x0 = Math.Max(0, (int)Math.Ceiling(xs[k] - 0.5f)), x1 = Math.Min(W - 1, (int)Math.Floor(xs[k + 1] - 0.5f));
                    for (int x = x0; x <= x1; x++) mask[y * W + x] = (byte)Math.Min(255, mask[y * W + x] + 128);
                }
            }
        return mask;
    }

    internal static byte[] Subtract(byte[] a, byte[] b)
    {
        var o = new byte[a.Length];
        for (int i = 0; i < a.Length; i++) o[i] = (byte)Math.Max(0, a[i] - b[i]);
        return o;
    }

    internal void FillMask(byte[] mask, Color32 c)
    {
        for (int i = 0; i < P.Length; i++)
        {
            byte m = mask[i];
            if (m == 0) continue;
            if (m == 255 && c.a == 255) P[i] = c;
            else Blend(i % W, i / W, c, m / 255f);
        }
    }

    // Small shapes fill within their own bounds rather than through a full-canvas mask.
    internal void FillPolygon(IList<Vector2> pts, Color32 c)
    {
        float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
        foreach (Vector2 p in pts) { minX = Mathf.Min(minX, p.x); minY = Mathf.Min(minY, p.y); maxX = Mathf.Max(maxX, p.x); maxY = Mathf.Max(maxY, p.y); }
        int y0 = Math.Max(0, (int)minY), y1 = Math.Min(H - 1, (int)maxY + 1);
        int bx0 = Math.Max(0, (int)minX), bx1 = Math.Min(W - 1, (int)maxX + 1);
        if (bx1 < bx0) return;
        var row = new float[bx1 - bx0 + 1];
        var xs = new List<float>();
        for (int y = y0; y <= y1; y++)
        {
            Array.Clear(row, 0, row.Length);
            for (int s = 0; s < 2; s++)
            {
                float sy = y + 0.25f + 0.5f * s;
                xs.Clear();
                for (int i = 0, j = pts.Count - 1; i < pts.Count; j = i++)
                {
                    Vector2 a = pts[i], b = pts[j];
                    if ((a.y <= sy && b.y > sy) || (b.y <= sy && a.y > sy)) xs.Add(a.x + (sy - a.y) / (b.y - a.y) * (b.x - a.x));
                }
                xs.Sort();
                for (int k = 0; k + 1 < xs.Count; k += 2)
                {
                    int x0 = Math.Max(bx0, (int)Math.Ceiling(xs[k] - 0.5f)), x1 = Math.Min(bx1, (int)Math.Floor(xs[k + 1] - 0.5f));
                    for (int x = x0; x <= x1; x++) row[x - bx0] += 0.5f;
                }
            }
            for (int x = bx0; x <= bx1; x++)
                if (row[x - bx0] > 0f) Blend(x, y, c, row[x - bx0]);
        }
    }

    internal void Disc(float cx, float cy, float r, Color32 c)
    {
        int x0 = (int)(cx - r - 1), x1 = (int)(cx + r + 1), y0 = (int)(cy - r - 1), y1 = (int)(cy + r + 1);
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                float d = Mathf.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy));
                float cov = Mathf.Clamp01(r + 0.5f - d);
                if (cov > 0f) Blend(x, y, c, cov);
            }
    }

    private float[]? cover;
    private readonly List<int> touched = new List<int>();

    // Thick antialiased polyline drawn as a coverage field, so overlapping segments do not darken.
    internal void Stroke(IList<Vector2> pts, float width, Color32 c, bool closed = false, byte[]? clip = null)
    {
        if (pts.Count < 2) return;
        cover ??= new float[W * H];
        float r = width / 2f;
        int n = closed ? pts.Count : pts.Count - 1;
        for (int s = 0; s < n; s++)
        {
            Vector2 a = pts[s], b = pts[(s + 1) % pts.Count];
            int x0 = Math.Max(0, (int)(Mathf.Min(a.x, b.x) - r - 1)), x1 = Math.Min(W - 1, (int)(Mathf.Max(a.x, b.x) + r + 1));
            int y0 = Math.Max(0, (int)(Mathf.Min(a.y, b.y) - r - 1)), y1 = Math.Min(H - 1, (int)(Mathf.Max(a.y, b.y) + r + 1));
            float abx = b.x - a.x, aby = b.y - a.y, len2 = Mathf.Max(1e-6f, abx * abx + aby * aby);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float px = x + 0.5f - a.x, py = y + 0.5f - a.y;
                    float t = Mathf.Clamp01((px * abx + py * aby) / len2);
                    float dx = px - abx * t, dy = py - aby * t;
                    float cov = r + 0.5f - Mathf.Sqrt(dx * dx + dy * dy);
                    if (cov <= 0f) continue;
                    if (cov > 1f) cov = 1f;
                    int i = y * W + x;
                    if (cover[i] == 0f) touched.Add(i);
                    if (cov > cover[i]) cover[i] = cov;
                }
        }
        foreach (int i in touched)
        {
            float m = clip == null ? 1f : clip[i] / 255f;
            if (m > 0f) Blend(i % W, i / W, c, cover[i] * m);
            cover[i] = 0f;
        }
        touched.Clear();
    }

    internal static int[] Indices(byte[] mask)
    {
        var list = new List<int>();
        for (int i = 0; i < mask.Length; i++) if (mask[i] > 0) list.Add(i);
        return list.ToArray();
    }

    // Composite a same-size layer only where the mask is set.
    internal void CompositeAt(CardCanvas src, int[] idx, byte[] mask)
    {
        foreach (int i in idx)
        {
            Color32 c = src.P[i];
            if (c.a != 0) Blend(i % W, i / W, c, mask[i] / 255f);
        }
    }

    internal void Grain(float strength, int seed, byte[]? mask = null)
    {
        var rnd = new System.Random(seed);
        for (int i = 0; i < P.Length; i++)
        {
            if (P[i].a == 0 || (mask != null && mask[i] == 0)) { rnd.Next(); continue; }
            int n = (int)((rnd.NextDouble() + rnd.NextDouble() - 1.0) * strength);
            Color32 c = P[i];
            P[i] = new Color32((byte)Mathf.Clamp(c.r + n, 0, 255), (byte)Mathf.Clamp(c.g + n, 0, 255), (byte)Mathf.Clamp(c.b + n, 0, 255), c.a);
        }
    }

    // Separable box blur, repeated, of the whole canvas (used for glows on small layers).
    internal void Blur(int radius, int passes = 2)
    {
        var tmp = new Color32[P.Length];
        for (int pass = 0; pass < passes; pass++)
        {
            BlurLine(P, tmp, W, H, radius, true);
            BlurLine(tmp, P, W, H, radius, false);
        }
    }

    private static void BlurLine(Color32[] src, Color32[] dst, int w, int h, int r, bool horizontal)
    {
        int lines = horizontal ? h : w, len = horizontal ? w : h;
        for (int l = 0; l < lines; l++)
        {
            int sr = 0, sg = 0, sb = 0, sa = 0, count = 0;
            for (int k = -r; k <= r; k++)
            {
                int q = Mathf.Clamp(k, 0, len - 1);
                Color32 c = src[horizontal ? l * w + q : q * w + l];
                sr += c.r * c.a; sg += c.g * c.a; sb += c.b * c.a; sa += c.a; count++;
            }
            for (int k = 0; k < len; k++)
            {
                int i = horizontal ? l * w + k : k * w + l;
                dst[i] = sa == 0 ? new Color32(0, 0, 0, 0)
                    : new Color32((byte)(sr / sa), (byte)(sg / sa), (byte)(sb / sa), (byte)(sa / count));
                int qo = Mathf.Clamp(k - r, 0, len - 1), qi = Mathf.Clamp(k + r + 1, 0, len - 1);
                Color32 o = src[horizontal ? l * w + qo : qo * w + l], n = src[horizontal ? l * w + qi : qi * w + l];
                sr += n.r * n.a - o.r * o.a; sg += n.g * n.a - o.g * o.a; sb += n.b * n.a - o.b * o.a; sa += n.a - o.a;
            }
        }
    }

    // Halve both dimensions by averaging 2x2 blocks; the card is drawn at twice its size.
    internal CardCanvas Half()
    {
        var o = new CardCanvas(W / 2, H / 2);
        for (int y = 0; y < o.H; y++)
            for (int x = 0; x < o.W; x++)
            {
                int r = 0, g = 0, b = 0, a = 0;
                for (int dy = 0; dy < 2; dy++)
                    for (int dx = 0; dx < 2; dx++)
                    {
                        Color32 c = P[(2 * y + dy) * W + 2 * x + dx];
                        r += c.r * c.a; g += c.g * c.a; b += c.b * c.a; a += c.a;
                    }
                o.P[y * o.W + x] = a == 0 ? new Color32(0, 0, 0, 0) : new Color32((byte)(r / a), (byte)(g / a), (byte)(b / a), (byte)(a / 4));
            }
        return o;
    }

    // Unity textures are bottom-up; the canvas is top-down.
    internal static CardCanvas FromTexture(Texture2D tex)
    {
        Color32[] src = tex.GetPixels32();
        var c = new CardCanvas(tex.width, tex.height);
        for (int y = 0; y < tex.height; y++) Array.Copy(src, (tex.height - 1 - y) * tex.width, c.P, y * tex.width, tex.width);
        return c;
    }

    internal Texture2D ToTexture()
    {
        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
        var dst = new Color32[P.Length];
        for (int y = 0; y < H; y++) Array.Copy(P, y * W, dst, (H - 1 - y) * W, W);
        tex.SetPixels32(dst);
        return tex;
    }

    internal CardCanvas Scaled(int w, int h)
    {
        var o = new CardCanvas(w, h);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float sx = (x + 0.5f) * W / w - 0.5f, sy = (y + 0.5f) * H / h - 0.5f;
                int x0 = Mathf.Clamp((int)sx, 0, W - 1), y0 = Mathf.Clamp((int)sy, 0, H - 1);
                int x1 = Math.Min(x0 + 1, W - 1), y1 = Math.Min(y0 + 1, H - 1);
                float fx = Mathf.Clamp01(sx - x0), fy = Mathf.Clamp01(sy - y0);
                Color32 a = Color32.Lerp(P[y0 * W + x0], P[y0 * W + x1], fx), b = Color32.Lerp(P[y1 * W + x0], P[y1 * W + x1], fx);
                o.P[y * w + x] = Color32.Lerp(a, b, fy);
            }
        return o;
    }
}
