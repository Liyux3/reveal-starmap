using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

namespace RevealStarMap.Runtime.Cards;

// Draws one collectible card at twice its size and halves it, following design/cards/mock6.py.
internal static class CardComposer
{
    internal const int CW = 750, CH = 1050, U = 2;
    private static readonly Color32 Ink = CardCanvas.Rgba(23, 22, 28), Cream = CardCanvas.Rgba(244, 236, 214),
        Gold = CardCanvas.Rgba(240, 196, 92), Body = CardCanvas.Rgba(37, 39, 51), Muted = CardCanvas.Rgba(160, 164, 176);
    private const int PanelTop = 650;

    private static readonly Dictionary<ProcGen.SubWorld.ZoneType, Color32> Biome = new Dictionary<ProcGen.SubWorld.ZoneType, Color32>
    {
        [ProcGen.SubWorld.ZoneType.Sandstone] = CardCanvas.Rgba(217, 180, 90), [ProcGen.SubWorld.ZoneType.Barren] = CardCanvas.Rgba(158, 145, 128),
        [ProcGen.SubWorld.ZoneType.BoggyMarsh] = CardCanvas.Rgba(126, 154, 78), [ProcGen.SubWorld.ZoneType.Forest] = CardCanvas.Rgba(94, 159, 87),
        [ProcGen.SubWorld.ZoneType.ToxicJungle] = CardCanvas.Rgba(178, 123, 169), [ProcGen.SubWorld.ZoneType.Ocean] = CardCanvas.Rgba(77, 143, 181),
        [ProcGen.SubWorld.ZoneType.FrozenWastes] = CardCanvas.Rgba(169, 210, 223), [ProcGen.SubWorld.ZoneType.IceCaves] = CardCanvas.Rgba(199, 223, 238),
        [ProcGen.SubWorld.ZoneType.MagmaCore] = CardCanvas.Rgba(200, 70, 46), [ProcGen.SubWorld.ZoneType.OilField] = CardCanvas.Rgba(106, 74, 56),
        [ProcGen.SubWorld.ZoneType.Wasteland] = CardCanvas.Rgba(192, 138, 92), [ProcGen.SubWorld.ZoneType.Rust] = CardCanvas.Rgba(201, 133, 58),
        [ProcGen.SubWorld.ZoneType.Swamp] = CardCanvas.Rgba(121, 166, 142), [ProcGen.SubWorld.ZoneType.Radioactive] = CardCanvas.Rgba(157, 212, 76),
        [ProcGen.SubWorld.ZoneType.Metallic] = CardCanvas.Rgba(142, 153, 166), [ProcGen.SubWorld.ZoneType.Moo] = CardCanvas.Rgba(169, 190, 106),
        [ProcGen.SubWorld.ZoneType.CrystalCaverns] = CardCanvas.Rgba(155, 123, 198),
    };

    internal static Color32 BiomeColour(ProcGen.SubWorld.ZoneType z) => Biome.TryGetValue(z, out Color32 c) ? c : CardCanvas.Rgba(130, 130, 150);

    private static float S(float v) => v * U;

    private static List<Vector2> Wobble(float x0, float y0, float x1, float y1, float r, int seed, float amp = 1.6f)
    {
        var rnd = new System.Random(seed);
        float p0 = (float)rnd.NextDouble() * 6.28f, p1 = (float)rnd.NextDouble() * 6.28f, p2 = (float)rnd.NextDouble() * 6.28f;
        var pts = new List<Vector2>();
        var corners = new[] { (x1 - r, y0 + r, -90f), (x1 - r, y1 - r, 0f), (x0 + r, y1 - r, 90f), (x0 + r, y0 + r, 180f) };
        foreach (var (cx, cy, a0) in corners)
            for (int k = 0; k < 10; k++)
            {
                float a = (a0 + 9 * k) * Mathf.Deg2Rad;
                pts.Add(new Vector2(cx + r * Mathf.Cos(a), cy + r * Mathf.Sin(a)));
            }
        for (int i = 0; i < pts.Count; i++)
        {
            float t = i / (float)pts.Count * 6.28f;
            float j = amp * U * (Mathf.Sin(3 * t + p0) + 0.6f * Mathf.Sin(7 * t + p1) + 0.3f * Mathf.Sin(13 * t + p2)) / 1.9f;
            pts[i] = new Vector2(pts[i].x + j, pts[i].y + j * 0.7f);
        }
        return pts;
    }

    private static void Inked(CardCanvas c, List<Vector2> pts, Color32 fill, float width)
    {
        c.FillPolygon(pts, fill);
        c.Stroke(pts, width * U, Ink, true);
    }

    private static CardCanvas Starfield(int w, int h, int seed)
    {
        var c = new CardCanvas(w, h, CardCanvas.Rgba(12, 14, 24));
        var rnd = new System.Random(seed);
        for (int i = 0; i < w * h / 900; i++)
            c.Disc(rnd.Next(w), rnd.Next(h), rnd.Next(4) == 0 ? 2 : 1, CardCanvas.Rgba(235, 236, 255, rnd.Next(90, 230)));
        return c;
    }

    private static Color32 Rainbow(float t, int alpha) => CardCanvas.Rgba(
        (int)(127 + 127 * Mathf.Sin(6.28f * t)), (int)(127 + 127 * Mathf.Sin(6.28f * t + 2.1f)), (int)(127 + 127 * Mathf.Sin(6.28f * t + 4.2f)), alpha);

    private static CardCanvas Holo(int w, int h, float seed, int[]? at = null)
    {
        var c = new CardCanvas(w, h);
        IEnumerable<int> cells = at ?? Enumerable.Range(0, w * h);
        foreach (int i in cells)
            {
                int x = i % w, y = i / w;
                float fx = x / (float)w, fy = y / (float)h;
                float t = (fx * 0.8f + fy * 0.6f) * 1.5f + seed / 6.28f;
                float band = Mathf.Pow(Mathf.Sin((fx * 0.7f - fy) * 6.28f * 2.2f) * 0.5f + 0.5f, 3f);
                c.P[y * w + x] = Rainbow(t, (int)(band * 70));
            }
        return c;
    }

    private static CardCanvas Guilloche(int w, int h, Func<int, Color32> colour, float density = 1f)
    {
        var c = new CardCanvas(w, h);
        foreach (var (cx, cy, R) in new[] { (w / 2f, h / 2f, Mathf.Max(w, h) * 0.62f), (0f, 0f, w * 0.32f), (w, 0f, w * 0.32f), (0f, h, w * 0.32f), (w, h, w * 0.32f) })
            for (int k = 0; k < (int)(9 * density); k++)
            {
                float r = R * (0.21f + 0.012f * k), d = R * (0.55f + 0.03f * k);
                var pts = new List<Vector2>(1600);
                for (int i = 0; i < 1600; i++)
                {
                    float t = i / 1600f * Mathf.PI * 14f;
                    pts.Add(new Vector2(cx + (R - r) * Mathf.Cos(t) + d * Mathf.Cos((R - r) / r * t), cy + (R - r) * Mathf.Sin(t) - d * Mathf.Sin((R - r) / r * t)));
                }
                c.Stroke(pts, U, colour(k));
            }
        for (int e = 0; e < (int)(14 * density); e++)
        {
            float off = S(8) + e * S(2.2f);
            var top = new List<Vector2>(); var bottom = new List<Vector2>(); var left = new List<Vector2>(); var right = new List<Vector2>();
            for (int i = 0; i <= 300; i++)
            {
                float x = w * i / 300f, y = h * i / 300f;
                float yy = off + S(3) * Mathf.Sin(x / S(11) + e * 0.6f), xx = off + S(3) * Mathf.Sin(y / S(11) + e * 0.6f);
                top.Add(new Vector2(x, yy)); bottom.Add(new Vector2(x, h - yy)); left.Add(new Vector2(xx, y)); right.Add(new Vector2(w - xx, y));
            }
            foreach (var line in new[] { top, bottom, left, right }) c.Stroke(line, U * 0.75f, colour(e));
        }
        return c;
    }

    private static CardCanvas Sparkles(int w, int h, int seed, int n, Func<float, Color32> colour)
    {
        var c = new CardCanvas(w, h);
        var rnd = new System.Random(seed);
        for (int i = 0; i < n; i++)
        {
            float x = rnd.Next(w), y = rnd.Next(h), r = new[] { 3, 4, 6, 9 }[rnd.Next(4)] * U;
            c.FillPolygon(new List<Vector2> { new Vector2(x, y - r), new Vector2(x + r * 0.25f, y - r * 0.25f), new Vector2(x + r, y),
                new Vector2(x + r * 0.25f, y + r * 0.25f), new Vector2(x, y + r), new Vector2(x - r * 0.25f, y + r * 0.25f),
                new Vector2(x - r, y), new Vector2(x - r * 0.25f, y - r * 0.25f) }, colour((float)rnd.NextDouble()));
        }
        return c;
    }

    private static CardCanvas Cracks(int w, int h, int seed, Color32 colour, int n, float step)
    {
        var c = new CardCanvas(w, h);
        var rnd = new System.Random(seed);
        for (int i = 0; i < n; i++)
        {
            float x = rnd.Next(w), y = rnd.Next(h), a = (float)rnd.NextDouble() * 6.28f;
            var pts = new List<Vector2> { new Vector2(x, y) };
            for (int k = rnd.Next(6, 16); k > 0; k--)
            {
                a += (float)(rnd.NextDouble() * 1.4 - 0.7);
                x += Mathf.Cos(a) * step * U; y += Mathf.Sin(a) * step * U;
                pts.Add(new Vector2(x, y));
            }
            c.Stroke(pts, S(2), colour);
        }
        return c;
    }

    private static CardCanvas Waves(int w, int h, Color32 colour)
    {
        var c = new CardCanvas(w, h);
        for (int k = 0; k < h; k += (int)S(9))
        {
            var pts = new List<Vector2>();
            for (int i = 0; i <= 200; i++) { float x = w * i / 200f; pts.Add(new Vector2(x, k + S(4) * Mathf.Sin(x / S(14) + k * 0.05f))); }
            c.Stroke(pts, S(2), colour);
        }
        return c;
    }

    private static void Star(CardCanvas c, float cx, float cy, float r, Color32 fill)
    {
        var pts = new List<Vector2>();
        for (int k = 0; k < 10; k++)
        {
            float a = -Mathf.PI / 2 + k * Mathf.PI / 5, rr = k % 2 == 0 ? r : r * 0.45f;
            pts.Add(new Vector2(cx + rr * Mathf.Cos(a), cy + rr * Mathf.Sin(a)));
        }
        c.FillPolygon(pts, fill);
        c.Stroke(pts, S(2), Ink, true);
    }

    private static void Diamond(CardCanvas c, float cx, float cy, float r, Color32 fill)
    {
        var pts = new List<Vector2> { new Vector2(cx, cy - r), new Vector2(cx + r * 0.9f, cy), new Vector2(cx, cy + r), new Vector2(cx - r * 0.9f, cy) };
        c.FillPolygon(pts, fill);
        c.Stroke(pts, S(2), Ink, true);
    }

    private static void Rosette(CardCanvas c, float cx, float cy, float r, Color32 colour)
    {
        for (int k = 0; k < 5; k++)
        {
            float rr = r * (0.55f + 0.09f * k);
            var pts = new List<Vector2>();
            for (int i = 0; i < 240; i++)
            {
                float t = i / 240f * 6.28f, m = 1 + 0.18f * Mathf.Cos(8 * t + k);
                pts.Add(new Vector2(cx + rr * Mathf.Cos(t) * m, cy + rr * Mathf.Sin(t) * m));
            }
            c.Stroke(pts, S(2), colour, true);
        }
        c.Disc(cx, cy, r * 0.3f, colour);
    }

    private static void Put(CardCanvas c, CardCanvas? art, float cx, float cy)
    {
        if (art != null) c.Composite(art, Mathf.RoundToInt(cx - art.W / 2f), Mathf.RoundToInt(cy - art.H / 2f));
    }

    private static TMP_FontAsset BodyFont => Localization.FontAsset;

    private static CardCanvas Label(string text, float px, Color32 colour, TMP_FontAsset? font = null) =>
        CardArt.Tint(CardArt.Text(text, font ?? BodyFont, px * U), colour);

    private static void Text(CardCanvas c, string text, float x, float y, float px, Color32 colour, bool right = false, bool centre = false)
    {
        CardCanvas t = Label(text, px, colour);
        float ox = right ? S(x) - t.W : centre ? S(x) - t.W / 2f : S(x);
        c.Composite(t, Mathf.RoundToInt(ox), Mathf.RoundToInt(S(y)));
    }

    internal static CardCanvas Compose(CardData card, CardCanvas? terrain)
    {
        CardFinish finish = card.Finish;
        Color32 lead = card.Biomes.Count > 0 ? BiomeColour(card.Biomes[0].Zone) : CardCanvas.Rgba(130, 130, 150);
        int seed = card.World.id;
        int W = CW * U, H = CH * U;
        var c = new CardCanvas(W, H);
        List<Vector2> outer = Wobble(S(6), S(6), S(CW - 6), S(CH - 6), S(40), seed);
        List<Vector2> inner = Wobble(S(34), S(34), S(CW - 34), S(CH - 34), S(26), seed + 2, 1f);
        byte[] outerMask = c.PolygonMask(outer), innerMask = c.PolygonMask(inner), band = CardCanvas.Subtract(outerMask, innerMask);
        int[] bandAt = CardCanvas.Indices(band), outerAt = CardCanvas.Indices(outerMask);

        // Frame: biome tint, or the finish's own colour, with grain.
        Color32 frame = CardCanvas.Mix(lead, CardCanvas.Rgba(60, 52, 44), 0.3f);
        Color32? repaint = finish switch
        {
            CardFinish.Aurora => CardCanvas.Rgba(16, 14, 22), CardFinish.Shadow => CardCanvas.Rgba(16, 14, 22),
            CardFinish.Etched => CardCanvas.Rgba(168, 174, 186), CardFinish.Frost => CardCanvas.Rgba(176, 214, 232),
            CardFinish.Molten => CardCanvas.Rgba(34, 26, 24), CardFinish.Tidal => CardCanvas.Rgba(28, 96, 118), _ => null
        };
        c.FillMask(outerMask, repaint ?? frame);
        c.Grain(14f, seed, band);
        bool holo = finish == CardFinish.Holo || finish == CardFinish.Aurora || finish == CardFinish.Gilded || finish == CardFinish.Golden;
        if (holo) c.CompositeAt(Holo(W, H, seed, bandAt), bandAt, band);
        switch (finish)
        {
            case CardFinish.Etched:
                c.CompositeAt(Guilloche(W, H, k => k % 2 == 0 ? CardCanvas.Rgba(214, 218, 228, 150) : CardCanvas.Rgba(150, 156, 170, 130), 0.7f), bandAt, band); break;
            case CardFinish.Gilded:
            case CardFinish.Golden:
                c.CompositeAt(Guilloche(W, H, k => k % 2 == 0 ? CardCanvas.Rgba(236, 196, 104, 215) : CardCanvas.Rgba(178, 132, 58, 190)), bandAt, band); break;
            case CardFinish.Aurora:
                CardCanvas lines = Guilloche(W, H, k => CardCanvas.Rgba(255, 255, 255, 230));
                CardCanvas iri = Holo(W, H, seed * 1.7f, bandAt);
                for (int i = 0; i < iri.P.Length; i++) { Color32 p = iri.P[i]; iri.P[i] = new Color32(p.r, p.g, p.b, lines.P[i].a); }
                c.CompositeAt(iri, bandAt, band);
                c.CompositeAt(Sparkles(W, H, seed, 90, t => Rainbow(t, 230)), bandAt, band); break;
            case CardFinish.Shadow:
                c.CompositeAt(Guilloche(W, H, k => k % 2 == 0 ? CardCanvas.Rgba(90, 255, 220, 200) : CardCanvas.Rgba(190, 90, 255, 200), 0.8f), bandAt, band); break;
            case CardFinish.Frost:
                c.CompositeAt(Cracks(W, H, seed, CardCanvas.Rgba(255, 255, 255, 235), 120, 8), bandAt, band);
                c.CompositeAt(Sparkles(W, H, seed + 9, 40, t => CardCanvas.Rgba(240, 250, 255, 220)), bandAt, band); break;
            case CardFinish.Molten:
                CardCanvas glow = Cracks(W, H, seed, CardCanvas.Rgba(255, 110, 30, 255), 110, 10);
                glow.Blur(3 * U, 1);
                c.CompositeAt(glow, bandAt, band);
                c.CompositeAt(Cracks(W, H, seed, CardCanvas.Rgba(255, 226, 140, 255), 110, 10), bandAt, band); break;
            case CardFinish.Tidal:
                c.CompositeAt(Waves(W, H, CardCanvas.Rgba(230, 248, 255, 210)), bandAt, band); break;
        }
        if (holo) c.CompositeAt(Sparkles(W, H, seed + 3, 50, t => Rainbow(t, 200)), bandAt, band);

        // Art: the revealed terrain covers the inner card above a starfield and darkens toward the facts.
        int iw = (int)S(CW - 68), ih = (int)S(CH - 68), ix = (int)S(34), iy = (int)S(34);
        CardCanvas art = Starfield(iw, ih, seed);
        if (terrain != null)
        {
            float s = Mathf.Max(iw / (float)terrain.W, S(PanelTop - 34 + 140) / terrain.H);
            if (terrain.H * s > ih) s = Mathf.Min(s, ih / (float)terrain.H);
            CardCanvas t = terrain.Scaled(Mathf.RoundToInt(terrain.W * s), Mathf.RoundToInt(terrain.H * s));
            int ox = (iw - t.W) / 2;
            art.Composite(t, ox, 0);
            float cell = t.W / (float)card.World.WorldSize.x;
            foreach (CardGeyser g in card.Geysers)
            {
                float gx = ox + (g.X - card.World.WorldOffset.x + 1) * cell, gy = (card.World.WorldSize.y - (g.Y - card.World.WorldOffset.y) - 1) * cell;
                if (gy > S(PanelTop - 40 - 34) || gy < S(120 - 34)) continue;
                if (g.Prefab == "GeyserGeneric")
                {
                    art.Disc(gx, gy, S(11), CardCanvas.Rgba(250, 244, 226, 230));
                    art.Stroke(Circle(gx, gy, S(11)), S(2), Ink, true);
                    Put(art, Label("?", 16, Ink), gx, gy);
                    continue;
                }
                CardCanvas? icon = CardArt.Fit(CardArt.Prefab(g.Prefab), (int)S(44));
                if (icon == null) continue;
                Put(art, CardArt.Rim(CardArt.Tint(icon, Cream), (int)S(2.5f), Cream), gx, gy);
                Put(art, icon, gx, gy);
            }
        }
        for (int y = 0; y < ih; y++)
        {
            float a = Mathf.Clamp01((y / (float)U + 34 - PanelTop + 200) / 260f) * 200f;
            if (a <= 0) continue;
            float k = a / 255f;
            for (int x = 0; x < iw; x++)
            {
                Color32 p = art.P[y * iw + x];
                art.P[y * iw + x] = new Color32((byte)(p.r + (14 - p.r) * k), (byte)(p.g + (15 - p.g) * k), (byte)(p.b + (22 - p.b) * k), p.a);
            }
        }
        if (finish == CardFinish.Aurora)
        {
            CardCanvas veil = Holo(iw, ih, seed * 3.1f);
            art.Composite(veil, 0, 0, null, 0.35f);
        }
        c.Composite(art, ix, iy, innerMask);
        c.Stroke(inner, S(4), Ink, true);
        c.Stroke(outer, S(6), Ink, true);
        if (finish == CardFinish.Gilded || finish == CardFinish.Golden || finish == CardFinish.Aurora)
            foreach (var (x, y) in new[] { (34f, 34f), (CW - 34f, 34f), (34f, CH - 34f), (CW - 34f, CH - 34f) })
                Rosette(c, S(x), S(y), S(24), CardCanvas.Rgba(240, 204, 112));

        // Title plate, planet badge and size.
        Inked(c, Wobble(S(118), S(40), S(CW - 40), S(122), S(18), seed + 3), CardCanvas.Rgba(24, 26, 36, 215), 4);
        bool latin = card.Name.All(ch => ch < 128);
        CardCanvas title = CardArt.Text(card.Name.ToUpperInvariant(), latin ? CardArt.Font("GRAYSTROKE REGULAR SDF") : BodyFont, S(50));
        title = finish switch
        {
            CardFinish.Gilded or CardFinish.Aurora or CardFinish.Golden => CardArt.Tint(title, CardCanvas.Rgba(246, 214, 128), CardCanvas.Rgba(206, 144, 58)),
            CardFinish.Shadow => CardArt.Tint(title, CardCanvas.Rgba(120, 255, 230), CardCanvas.Rgba(120, 195, 210)),
            CardFinish.Etched => CardArt.Tint(title, CardCanvas.Rgba(236, 238, 244), CardCanvas.Rgba(186, 188, 204)),
            _ => title
        };
        title = CardArt.Rim(title, (int)S(3), Ink);
        if (title.W > S(CW - 340)) title = title.Scaled((int)S(CW - 340), Mathf.RoundToInt(title.H * S(CW - 340) / title.W));
        c.Composite(title, (int)S(184), Mathf.RoundToInt(S(81) - title.H / 2f));
        Text(c, $"{card.World.WorldSize.x}×{card.World.WorldSize.y}", CW - 62, 68, 22, Cream, right: true);
        c.Disc(S(92), S(84), S(64), CardCanvas.Mix(lead, CardCanvas.Rgba(20, 22, 30), 0.5f));
        c.Stroke(Circle(S(92), S(84), S(64)), S(5), Ink, true);
        Put(c, CardArt.Fit(CardArt.WorldSprite(card.World), (int)S(112)), S(92), S(84));

        // Facts: kind and rarity, geyser tiles, flavor, collector line.
        string kind = card.Start ? RevealText.Get("card.start") : card.Traits.Length > 0 ? string.Join(" · ", card.Traits.Take(2)) : RevealText.Get("card.asteroid");
        Text(c, kind, 52, PanelTop + 8, 24, Cream);
        RarityMarks(c, card, CW - 54, PanelTop + 22);
        c.Stroke(new List<Vector2> { new Vector2(S(50), S(PanelTop + 50)), new Vector2(S(CW - 50), S(PanelTop + 50)) }, S(2), CardCanvas.Mix(lead, Cream, 0.4f));
        List<CardGeyser> tiles = card.Tiles();
        float tw = (CW - 100 - 5 * 12) / 6f, ty = PanelTop + 66;
        for (int i = 0; i < Math.Min(6, tiles.Count); i++)
        {
            CardGeyser g = tiles[i];
            float x = 50 + i * (tw + 12);
            Inked(c, Wobble(S(x), S(ty), S(x + tw), S(ty + tw), S(14), seed * 10 + i), CardCanvas.Rgba(40, 43, 56, 235), 3);
            CardCanvas? icon = CardArt.Fit(CardArt.Prefab(g.Prefab == "GeyserGeneric" ? "GeyserGeneric_steam" : g.Prefab), (int)S(tw - 18));
            if (icon != null && g.Prefab == "GeyserGeneric") icon = CardArt.Tint(icon, CardCanvas.Rgba(112, 117, 130));
            Put(c, icon, S(x + tw / 2), S(ty + tw / 2));
            if (g.Count > 1)
            {
                c.Disc(S(x + tw - 8), S(ty + 10), S(17), Gold);
                c.Stroke(Circle(S(x + tw - 8), S(ty + 10), S(17)), S(3), Ink, true);
                Put(c, Label(g.Count.ToString(), 20, Ink), S(x + tw - 8), S(ty + 10));
            }
            string output = g.GramsPerSecond.HasValue ? $"{g.GramsPerSecond.Value:N0} g/s" : "?";
            Text(c, output, x + tw / 2, ty + tw + 6, 17, g.GramsPerSecond.HasValue ? Cream : Muted, centre: true);
        }
        float yText = ty + tw + 40;
        if (tiles.Count > 6) { Text(c, RevealText.Format("card.more", tiles.Count - 6), 52, yText, 17, Muted); yText += 28; }
        if (!string.IsNullOrEmpty(card.Description) && yText < 960)
        {
            CardCanvas flavor = Label(card.Description, 19, CardCanvas.Rgba(196, 190, 174));
            if (flavor.W > S(CW - 110)) flavor = flavor.Scaled((int)S(CW - 110), Mathf.RoundToInt(flavor.H * S(CW - 110) / flavor.W));
            c.Composite(flavor, (int)S(52), (int)S(Mathf.Max(yText, 926)));
        }
        Text(c, $"{card.SetCode}  ·  {Number(card)}", 52, 984, 17, Muted);
        Text(c, card.Seed, CW - 52, 987, 14, CardCanvas.Rgba(130, 134, 146), right: true);
        Text(c, $"No. {card.Serial:0000}", CW - 50, 958, 15, CardCanvas.Rgba(200, 194, 176), right: true);

        CardCanvas result = c.Half();
        WholeCard(result, finish);
        Stamps(result, card, finish);
        return result;
    }

    internal static string Number(CardData card)
    {
        string label = card.Finish switch
        {
            CardFinish.Plain => "COMMON", CardFinish.Holo => "RARE", CardFinish.Gilded => "ULTRA RARE", CardFinish.Aurora => "SECRET",
            CardFinish.Golden => "GOLD SECRET", CardFinish.Shadow => "SHADOW SECRET", CardFinish.Misprint => "MISPRINT", _ => "UNCOMMON"
        };
        int number = CardData.IsHidden(card.Finish) ? card.SetSize + card.HiddenRank : card.Index;
        return $"{number:00}/{card.SetSize:00}  {label}";
    }

    private static List<Vector2> Circle(float cx, float cy, float r)
    {
        var pts = new List<Vector2>();
        for (int i = 0; i < 72; i++) pts.Add(new Vector2(cx + r * Mathf.Cos(i * 6.2832f / 72), cy + r * Mathf.Sin(i * 6.2832f / 72)));
        return pts;
    }

    private static void RarityMarks(CardCanvas c, CardData card, float right, float cy)
    {
        string[] rank = { "Common", "Uncommon", "Rare", "Ultra" };
        int n = CardData.IsHidden(card.Finish) ? 5 : Array.IndexOf(rank, card.Tier) + 1;
        for (int k = 0; k < n; k++)
        {
            float x = S(right - 11 - k * 26), y = S(cy);
            if (n >= 4) Star(c, x, y, S(13), Gold);
            else Diamond(c, x, y, S(11), n == 1 ? CardCanvas.Rgba(200, 200, 210) : Gold);
        }
    }

    private static void WholeCard(CardCanvas c, CardFinish finish)
    {
        for (int i = 0; i < c.P.Length; i++)
        {
            Color32 p = c.P[i];
            if (p.a == 0) continue;
            float l = (p.r + p.g + p.b) / 765f;
            if (finish == CardFinish.Golden)
            {
                Color32 g = l < 0.5f ? CardCanvas.Mix(CardCanvas.Rgba(58, 36, 8), CardCanvas.Rgba(196, 148, 64), l * 2) : CardCanvas.Mix(CardCanvas.Rgba(196, 148, 64), CardCanvas.Rgba(255, 226, 150), l * 2 - 1);
                c.P[i] = new Color32((byte)(p.r * 0.22f + g.r * 0.78f), (byte)(p.g * 0.22f + g.g * 0.78f), (byte)(p.b * 0.22f + g.b * 0.78f), p.a);
            }
            else if (finish == CardFinish.Shadow)
                c.P[i] = new Color32((byte)(p.r * 0.35f + (30 + 60 * l) * 0.65f), (byte)(p.g * 0.35f + (20 + 90 * l) * 0.65f), (byte)(p.b * 0.35f + (60 + 170 * l) * 0.65f), p.a);
        }
        if (finish != CardFinish.Misprint) return;
        var src = (Color32[])c.P.Clone();
        for (int y = 0; y < c.H; y++)
            for (int x = 0; x < c.W; x++)
            {
                int i = y * c.W + x;
                Color32 r = src[Mathf.Clamp(y - 3, 0, c.H - 1) * c.W + Mathf.Clamp(x - 7, 0, c.W - 1)];
                Color32 b = src[Mathf.Clamp(y + 2, 0, c.H - 1) * c.W + Mathf.Clamp(x + 6, 0, c.W - 1)];
                c.P[i] = new Color32(r.r, src[i].g, b.b, src[i].a);
            }
    }

    private static void InkStamp(CardCanvas c, string text, float cx, float cy, Color32 colour, float angle, int seed, float size, bool paper = false)
    {
        CardCanvas t = CardArt.Tint(CardArt.Text(text, BodyFont, size), colour);
        int pad = 18;
        var s = new CardCanvas(t.W + 2 * pad, t.H + 2 * pad);
        if (paper) for (int i = 0; i < s.P.Length; i++) s.P[i] = CardCanvas.Rgba(240, 232, 206);
        var box = new List<Vector2> { new Vector2(3, 3), new Vector2(s.W - 4, 3), new Vector2(s.W - 4, s.H - 4), new Vector2(3, s.H - 4) };
        s.Stroke(box, 4, colour, true);
        s.Composite(t, pad, pad);
        var rnd = new System.Random(seed);
        for (int i = 0; i < s.P.Length; i++)
            if (s.P[i].a > 0 && rnd.NextDouble() < 0.1) s.P[i].a = (byte)(s.P[i].a * 0.2f);
        // Rotate by sampling back into a square target.
        int side = (int)Mathf.Sqrt(s.W * s.W + s.H * s.H) + 2;
        var r = new CardCanvas(side, side);
        float ca = Mathf.Cos(angle * Mathf.Deg2Rad), sa = Mathf.Sin(angle * Mathf.Deg2Rad);
        for (int y = 0; y < side; y++)
            for (int x = 0; x < side; x++)
            {
                float dx = x - side / 2f, dy = y - side / 2f;
                int sx = Mathf.RoundToInt(dx * ca + dy * sa + s.W / 2f), sy = Mathf.RoundToInt(-dx * sa + dy * ca + s.H / 2f);
                if ((uint)sx < (uint)s.W && (uint)sy < (uint)s.H) r.P[y * side + x] = s.P[sy * s.W + sx];
            }
        c.Composite(r, Mathf.RoundToInt(cx - side / 2f), Mathf.RoundToInt(cy - side / 2f), null, 0.92f);
    }

    private static void WaxSeal(CardCanvas c, float cx, float cy, float r, int seed)
    {
        var rnd = new System.Random(seed);
        var pts = new List<Vector2>();
        for (int i = 0; i < 40; i++)
        {
            float a = i / 40f * 6.2832f, rr = r + (float)(rnd.NextDouble() * 12 - 5);
            pts.Add(new Vector2(cx + rr * Mathf.Cos(a), cy + rr * Mathf.Sin(a)));
        }
        c.FillPolygon(pts.Select(p => p + new Vector2(4, 6)).ToList(), CardCanvas.Rgba(0, 0, 0, 110));
        c.FillPolygon(pts, CardCanvas.Rgba(150, 26, 34));
        c.Disc(cx, cy, r * 0.72f, CardCanvas.Rgba(122, 18, 26));
        c.Stroke(Circle(cx, cy, r * 0.72f), 3, CardCanvas.Rgba(186, 60, 64), true);
        CardCanvas? pod = CardArt.Fit(CardArt.Prefab("Headquarters"), (int)(r * 1.1f));
        if (pod != null) Put(c, CardArt.Tint(pod, CardCanvas.Rgba(90, 10, 18, 200)), cx, cy);
        c.Disc(cx - r * 0.3f, cy - r * 0.5f, r * 0.28f, CardCanvas.Rgba(255, 210, 200, 50));
    }

    private static void Stamps(CardCanvas c, CardData card, CardFinish finish)
    {
        float tilt = finish == CardFinish.Misprint ? 172f : -9f;
        if (card.Revealed) InkStamp(c, "REVEALED", CW - 200, 590, CardCanvas.Rgba(196, 36, 44), tilt, card.World.id, 38);
        else InkStamp(c, "SURVEYED", CW - 200, 590, CardCanvas.Rgba(40, 80, 170), tilt, card.World.id, 38);
        if (card.Start) WaxSeal(c, CW - 96, 196, 48, card.World.id);
        if (card.Traits.Any(t => t.IndexOf("Satellite", StringComparison.OrdinalIgnoreCase) >= 0))
            InkStamp(c, "CLASSIFIED", 200, 220, CardCanvas.Rgba(34, 30, 36), 7, card.World.id + 5, 32, paper: true);
        if (card.Cycle <= 1)
        {
            c.Disc(CW - 92, 600, 26, CardCanvas.Rgba(24, 22, 28, 230));
            c.Stroke(Circle(CW - 92, 600, 26), 3, CardCanvas.Rgba(240, 236, 220), true);
            Put(c, CardArt.Tint(CardArt.Text("1st", BodyFont, 16), CardCanvas.Rgba(240, 236, 220)), CW - 92, 600);
        }
        if (card.Cycle >= 1000) InkStamp(c, "VETERAN", 180, 600, CardCanvas.Rgba(70, 60, 40), 12, card.World.id + 7, 30);
        string digits = new string(card.Seed.Where(char.IsDigit).ToArray());
        if (System.Text.RegularExpressions.Regex.IsMatch(digits, @"(\d)\1\1"))
            InkStamp(c, "LUCKY SEED", 190, 980, CardCanvas.Rgba(40, 140, 70), -4, card.World.id + 11, 22);
    }
}
