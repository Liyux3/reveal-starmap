# Collectible asteroid cards, third pass: the revealed terrain is the art, Klei-like ink frames.
# usage: uv run --with pillow --with numpy mock4.py RUN_DIR OUT_DIR
import math, os, random, re, glob
import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageChops
import mock, mock2, mock3
from mock2 import sprite, headline, NAMES
from mock3 import info, flavor, geyser_icon, starfield, SET, WORLDS, D

OUT = mock2.OUT
RUN = mock2.RUN
U = 2                      # draw at twice the size, then downsample for smooth ink
CW, CH = 750, 1050
INKC = (23, 22, 28)
CREAM = (244, 236, 214)
GOLD = (240, 196, 92)


def S(*v): return tuple(round(x * U) for x in v)


def wobble_box(box, r, seed, amp=1.6):
    """Points of a rounded rectangle with a slow hand-drawn wobble."""
    x0, y0, x1, y1 = box
    rnd = random.Random(seed)
    ph = [rnd.random() * 6.28 for _ in range(3)]
    pts = []
    corners = [(x1 - r, y0 + r, -90), (x1 - r, y1 - r, 0), (x0 + r, y1 - r, 90), (x0 + r, y0 + r, 180)]
    for cx, cy, a0 in corners:
        for k in range(10):
            a = math.radians(a0 + 9 * k)
            pts.append((cx + r * math.cos(a), cy + r * math.sin(a)))
    out = []
    for i, (x, y) in enumerate(pts):
        t = i / len(pts) * 6.28
        j = amp * U * (math.sin(3 * t + ph[0]) + 0.6 * math.sin(7 * t + ph[1]) + 0.3 * math.sin(13 * t + ph[2])) / 1.9
        out.append((x + j, y + j * 0.7))
    return out


def inked(d, box, r, fill, seed, width=5):
    pts = wobble_box(box, r, seed)
    d.polygon(pts, fill=fill)
    d.line(pts + pts[:1], fill=INKC, width=width * U, joint="curve")


def grain(im, strength=10, seed=0):
    rnd = np.random.default_rng(seed)
    n = rnd.normal(0, strength, (im.height, im.width, 1))
    a = np.asarray(im).astype(float)
    a[..., :3] = np.clip(a[..., :3] + n, 0, 255)
    return Image.fromarray(a.astype(np.uint8), im.mode)


def outlined(text_img, px=3):
    """White header type with a dark ink rim, the way the game sets titles on art."""
    a = text_img.split()[-1]
    rim = a.filter(ImageFilter.MaxFilter(px * 2 + 1))
    out = Image.new("RGBA", text_img.size, INKC + (0,))
    out.putalpha(rim)
    out.alpha_composite(text_img)
    return out


def terrain_art(w):
    hit = [p for p in glob.glob(os.path.join(RUN, "renders", "*.png")) + glob.glob(os.path.join(RUN, "exports", "*.png"))
           if f" - {w['name']} - " in os.path.basename(p)]
    if not hit: return None
    im = Image.open(hit[0]).convert("RGB")
    wc, hc = w["w"], w["h"]
    # The export is margin + map + caption with whole pixels per cell; find the density that fits.
    px, m = next((p, (im.width - wc * p) // 2) for p in range(16, 0, -1)
                 if im.width > wc * p and abs(im.height - hc * p - 4 * ((im.width - wc * p) // 2)) <= 2)
    return im.crop((m, m, m + round(wc * px), m + round(hc * px))), px


def holo(size, seed):
    """A soft rainbow sheen for the rarest cards."""
    w, h = size
    x = np.linspace(0, 1, w)[None, :]
    y = np.linspace(0, 1, h)[:, None]
    t = (x * 0.8 + y * 0.6) * 6.28 * 1.5 + seed
    rgb = np.stack([np.sin(t) * 0.5 + 0.5, np.sin(t + 2.1) * 0.5 + 0.5, np.sin(t + 4.2) * 0.5 + 0.5], -1)
    band = (np.sin((x * 0.7 - y) * 6.28 * 2.2) * 0.5 + 0.5) ** 3
    a = (band * 70).astype(np.uint8)
    im = Image.fromarray((rgb * 255).astype(np.uint8), "RGB").convert("RGBA")
    im.putalpha(Image.fromarray(a))
    return im


def card(w, index):
    shares, gs, traits = info(w)
    lead = shares[0][1] if shares else (130, 130, 150)
    frame = mock.mix(lead, (60, 52, 44), 0.30)
    body = (37, 39, 51)
    im = Image.new("RGBA", S(CW, CH), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    inked(d, S(6, 6, CW - 6, CH - 6), 40 * U, frame + (255,), w["id"], 6)
    im = grain(im, 7, w["id"])
    d = ImageDraw.Draw(im)
    if w["start"] or len({g["prefab"] for g in gs}) >= 5:
        sheen = holo(im.size, w["id"])
        mask = Image.new("L", im.size, 0)
        ImageDraw.Draw(mask).polygon(wobble_box(S(6, 6, CW - 6, CH - 6), 40 * U, w["id"]), fill=255)
        ImageDraw.Draw(mask).rectangle(S(30, 150, CW - 30, CH - 30), fill=0)
        im.paste(Image.alpha_composite(im, sheen), (0, 0), mask)
        d = ImageDraw.Draw(im)
    inked(d, S(28, 140, CW - 28, CH - 28), 22 * U, body + (255,), w["id"] + 1, 4)
    # Art window: the revealed asteroid as the game draws it, whole, on a starfield.
    art_box = (52, 164, CW - 52, 640)
    aw, ah = art_box[2] - art_box[0], art_box[3] - art_box[1]
    sky = starfield(S(aw, ah), w["id"], (12, 14, 24))
    glow = Image.new("RGBA", sky.size, (0, 0, 0, 0))
    ImageDraw.Draw(glow).ellipse((sky.width * 0.2, sky.height * 0.15, sky.width * 0.8, sky.height * 0.85), fill=lead + (70,))
    sky.alpha_composite(glow.filter(ImageFilter.GaussianBlur(60 * U)))
    art = terrain_art(w)
    if art:
        t, tpx = art
        s = min((sky.width - 40 * U) / t.width, (sky.height - 40 * U) / t.height)
        tt = t.resize((round(t.width * s), round(t.height * s)), Image.LANCZOS).convert("RGBA")
        ox, oy = (sky.width - tt.width) // 2, (sky.height - tt.height) // 2
        shadow = Image.new("RGBA", sky.size, (0, 0, 0, 0))
        ImageDraw.Draw(shadow).rectangle((ox + 8 * U, oy + 10 * U, ox + tt.width + 8 * U, oy + tt.height + 10 * U), fill=(0, 0, 0, 140))
        sky.alpha_composite(shadow.filter(ImageFilter.GaussianBlur(8 * U)))
        sky.alpha_composite(tt, (ox, oy))
        sd = ImageDraw.Draw(sky)
        sd.rectangle((ox - 2 * U, oy - 2 * U, ox + tt.width + 2 * U, oy + tt.height + 2 * U), outline=INKC, width=3 * U)
        cell = tt.width / w["w"]
        for g in gs:
            icon = geyser_icon(g["prefab"], round(34 * U))
            if not icon: continue
            gx, gy = ox + (g["x"] - w["x"] + 1) * cell, oy + (w["h"] - (g["y"] - w["y"]) - 1) * cell
            sd.ellipse((gx - 22 * U, gy - 22 * U, gx + 22 * U, gy + 22 * U), fill=(250, 246, 232, 235), outline=INKC, width=2 * U)
            sky.alpha_composite(icon, (round(gx - icon.width / 2), round(gy - icon.height / 2)))
    mask = Image.new("L", sky.size, 0)
    ImageDraw.Draw(mask).polygon([(x - art_box[0] * U, y - art_box[1] * U) for x, y in wobble_box(S(*art_box), 14 * U, w["id"] + 2, 1.0)], fill=255)
    im.paste(sky, S(art_box[0], art_box[1]), mask)
    d.line(wobble_box(S(*art_box), 14 * U, w["id"] + 2, 1.0) + wobble_box(S(*art_box), 14 * U, w["id"] + 2, 1.0)[:1], fill=INKC, width=4 * U)
    # Name banner across the top, with the planet badge on its left.
    inked(d, S(120, 34, CW - 34, 124), 18 * U, mock.mix(lead, CREAM, 0.55) + (255,), w["id"] + 3, 5)
    name = outlined(headline(w["name"].upper(), 50 * U), 3 * U)
    name = mock3.fit(name, (CW - 330) * U)
    im.alpha_composite(name, (190 * U, round(79 * U - name.height / 2)))
    size_text = f"{w['w']}×{w['h']}"
    f = mock.font(22 * U)
    d.text(((CW - 58) * U - d.textlength(size_text, font=f), 64 * U), size_text, font=f, fill=INKC)
    cx, cy, r = 92 * U, 86 * U, 66 * U
    d.ellipse((cx - r, cy - r, cx + r, cy + r), fill=mock.mix(lead, (20, 22, 30), 0.55), outline=INKC, width=5 * U)
    p = sprite(f"world-{w['id']}", 512)
    if p:
        s = (r * 1.75) / max(p.width, p.height)
        p = p.resize((round(p.width * s), round(p.height * s)), Image.LANCZOS)
        im.alpha_composite(p, (round(cx - p.width / 2), round(cy - p.height / 2)))
    # Type line with rarity.
    kind = "Start Asteroid" if w["start"] else (" · ".join(traits[:2]) or "Asteroid")
    inked(d, S(46, 656, CW - 46, 704), 12 * U, mock.mix(lead, body, 0.62) + (255,), w["id"] + 4, 3)
    d.text((66 * U, 665 * U), kind, font=mock.font(22 * U), fill=CREAM)
    mock3.rarity(d, (CW - 66) * U, 680 * U, w, gs, GOLD) if U == 1 else rarity2(d, CW - 66, 680, w, gs)
    # Geyser tiles: one per kind, with a count badge and the output once analyzed.
    kinds, order = {}, []
    for g in gs:
        k = g["prefab"]
        if k not in kinds: kinds[k] = dict(g, count=0); order.append(k)
        kinds[k]["count"] += 1
    tiles = [kinds[k] for k in order][:6]
    tx, ty, tw = 52, 722, (CW - 104 - 5 * 12) / 6
    for i, g in enumerate(tiles):
        x = tx + i * (tw + 12)
        inked(d, S(x, ty, x + tw, ty + tw), 14 * U, (52, 55, 70, 255), w["id"] * 10 + i, 3)
        icon = geyser_icon(g["prefab"], round((tw - 18) * U))
        if icon: im.alpha_composite(icon, (round((x + tw / 2) * U - icon.width / 2), round((ty + tw / 2) * U - icon.height / 2)))
        if g["count"] > 1:
            bx, by = (x + tw - 8) * U, (ty + 10) * U
            d.ellipse((bx - 17 * U, by - 17 * U, bx + 17 * U, by + 17 * U), fill=GOLD, outline=INKC, width=3 * U)
            t = f"{g['count']}"
            d.text((bx - d.textlength(t, font=mock.font(20 * U)) / 2, by - 15 * U), t, font=mock.font(20 * U), fill=INKC)
        label = f"{g['average'] * 1000:,.0f} g/s" if "average" in g else "?"
        f = mock.font(17 * U)
        d.text(((x + tw / 2) * U - d.textlength(label, font=f) / 2, (ty + tw + 6) * U), label, font=f, fill=CREAM if "average" in g else (140, 144, 158))
    if len(order) > 6:
        d.text((52 * U, (ty + tw + 34) * U), f"+ {len(order) - 6} more kinds", font=mock.font(17 * U), fill=(150, 154, 166))
    text = flavor(w)
    if text:
        f = mock.font(19 * U)
        lines, line = [], ""
        for word in text.split():
            trial = (line + " " + word).strip()
            if d.textlength(trial, font=f) > (CW - 120) * U: lines.append(line); line = word
            else: line = trial
        lines.append(line)
        for k, ln in enumerate(lines[:2]):
            d.text((60 * U, (918 + k * 28) * U), ln, font=f, fill=(176, 172, 160))
    foot = f"{SET}  ·  {index:02d}/{len(WORLDS):02d}"
    d.text((60 * U, 984 * U), foot, font=mock.font(17 * U), fill=(150, 154, 166))
    seed = D["seed"]
    d.text(((CW - 60) * U - d.textlength(seed, font=mock.font(14 * U)), 987 * U), seed, font=mock.font(14 * U), fill=(120, 124, 136))
    return im.resize((CW, CH), Image.LANCZOS)


DECORATE = None
NUMBER = None
TITLE = None


def card_full(w, index):
    """Full-art card: the revealed terrain fills the card, title and facts float over it."""
    shares, gs, traits = info(w)
    lead = shares[0][1] if shares else (130, 130, 150)
    frame = mock.mix(lead, (60, 52, 44), 0.30)
    im = Image.new("RGBA", S(CW, CH), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    outer = wobble_box(S(6, 6, CW - 6, CH - 6), 40 * U, w["id"])
    d.polygon(outer, fill=frame + (255,))
    im = grain(im, 7, w["id"])
    rare = HOLO(w, gs) if "HOLO" in globals() and HOLO else (w["start"] or len({g["prefab"] for g in gs}) >= 5)
    if rare:
        sheen = holo(im.size, w["id"])
        mask = Image.new("L", im.size, 0)
        ImageDraw.Draw(mask).polygon(outer, fill=255)
        im.paste(Image.alpha_composite(im, sheen), (0, 0), mask)
    d = ImageDraw.Draw(im)
    # Art: cover the inner card with the terrain, starfield where the world is shorter.
    inner = (34, 34, CW - 34, CH - 34)
    iw, ih = (inner[2] - inner[0]) * U, (inner[3] - inner[1]) * U
    art = Image.alpha_composite(starfield((iw, ih), w["id"], (12, 14, 24)), Image.new("RGBA", (iw, ih), (0, 0, 0, 0)))
    panel_top = 650
    t = terrain_art(w)
    if t:
        t, _ = t
        s = max(iw / t.width, (panel_top - 24 + 140) * U / t.height)
        s = min(s, ih / t.height * 1.0) if t.height * s > ih else s
        tt = t.resize((round(t.width * s), round(t.height * s)), Image.LANCZOS).convert("RGBA")
        ox, oy = (iw - tt.width) // 2, 0
        art.alpha_composite(tt, (ox, oy))
        cell = tt.width / w["w"]
        ad = ImageDraw.Draw(art)
        for g in gs:
            gx = ox + (g["x"] - w["x"] + 1) * cell
            gy = oy + (w["h"] - (g["y"] - w["y"]) - 1) * cell
            if gy > (panel_top - 40) * U or gy < 120 * U: continue
            if g["prefab"] == "GeyserGeneric":
                ad.ellipse((gx - 11 * U, gy - 11 * U, gx + 11 * U, gy + 11 * U), fill=(250, 244, 226, 230), outline=INKC, width=2 * U)
                f = mock.font(16 * U)
                ad.text((gx - ad.textlength("?", font=f) / 2, gy - 12 * U), "?", font=f, fill=INKC)
                continue
            icon = geyser_icon(g["prefab"], 44 * U)
            if not icon: continue
            a = icon.split()[-1].filter(ImageFilter.MaxFilter(5 * U + 1))
            rim = Image.new("RGBA", icon.size, (250, 244, 226, 0)); rim.putalpha(a)
            art.alpha_composite(rim, (round(gx - icon.width / 2), round(gy - icon.height / 2)))
            art.alpha_composite(icon, (round(gx - icon.width / 2), round(gy - icon.height / 2)))
    # Darken toward the bottom so the facts panel reads over the art.
    fade = Image.new("L", (1, ih), 0)
    for y in range(ih):
        fade.putpixel((0, y), int(max(0, min(1, (y / U - panel_top + 200) / 260)) * 200))
    shade = Image.new("RGBA", (iw, ih), (14, 15, 22, 255)); shade.putalpha(fade.resize((iw, ih)))
    art.alpha_composite(shade)
    mask = Image.new("L", art.size, 0)
    ImageDraw.Draw(mask).polygon([(x - inner[0] * U, y - inner[1] * U) for x, y in wobble_box(S(*inner), 26 * U, w["id"] + 2, 1.0)], fill=255)
    im.paste(art, S(inner[0], inner[1]), mask)
    inner_poly = wobble_box(S(*inner), 26 * U, w["id"] + 2, 1.0)
    if DECORATE:
        im = DECORATE(im, outer, inner_poly, w, gs)
        d = ImageDraw.Draw(im)
    d.line(inner_poly + inner_poly[:1], fill=INKC, width=4 * U)
    d.line(outer + outer[:1], fill=INKC, width=6 * U)
    # Title plate.
    plate = Image.new("RGBA", im.size, (0, 0, 0, 0))
    pd = ImageDraw.Draw(plate)
    inked(pd, S(118, 40, CW - 40, 122), 18 * U, (24, 26, 36, 215), w["id"] + 3, 4)
    im.alpha_composite(plate)
    d = ImageDraw.Draw(im)
    head = headline(w["name"].upper(), 50 * U)
    if TITLE: head = TITLE(head, w, gs)
    name = mock3.fit(outlined(head, 3 * U), (CW - 340) * U)
    im.alpha_composite(name, (184 * U, round(81 * U - name.height / 2)))
    size_text = f"{w['w']}\u00d7{w['h']}"
    f = mock.font(22 * U)
    d.text(((CW - 62) * U - d.textlength(size_text, font=f), 66 * U), size_text, font=f, fill=CREAM)
    cx, cy, r = 92 * U, 84 * U, 64 * U
    d.ellipse((cx - r, cy - r, cx + r, cy + r), fill=mock.mix(lead, (20, 22, 30), 0.5), outline=INKC, width=5 * U)
    p = sprite(f"world-{w['id']}", 512)
    if p:
        s = (r * 1.75) / max(p.width, p.height)
        p = p.resize((round(p.width * s), round(p.height * s)), Image.LANCZOS)
        im.alpha_composite(p, (round(cx - p.width / 2), round(cy - p.height / 2)))
    # Facts panel.
    kind = "Start Asteroid" if w["start"] else (" \u00b7 ".join(traits[:2]) or "Asteroid")
    d.text((52 * U, (panel_top + 6) * U), kind, font=mock.font(24 * U), fill=CREAM)
    rarity2(d, CW - 54, panel_top + 22, w, gs)
    d.line(S(50, panel_top + 50, CW - 50, panel_top + 50), fill=mock.mix(lead, CREAM, 0.4), width=2 * U)
    kinds, order = {}, []
    for g in gs:
        k = g["prefab"]
        if k not in kinds: kinds[k] = dict(g, count=0); order.append(k)
        kinds[k]["count"] += 1
    tiles = [kinds[k] for k in order][:6]
    tx, ty, tw = 50, panel_top + 66, (CW - 100 - 5 * 12) / 6
    for i, g in enumerate(tiles):
        x = tx + i * (tw + 12)
        inked(d, S(x, ty, x + tw, ty + tw), 14 * U, (40, 43, 56, 235), w["id"] * 10 + i, 3)
        icon = geyser_icon(g["prefab"], round((tw - 18) * U))
        if icon: im.alpha_composite(icon, (round((x + tw / 2) * U - icon.width / 2), round((ty + tw / 2) * U - icon.height / 2)))
        if g["count"] > 1:
            bx, by = (x + tw - 8) * U, (ty + 10) * U
            d.ellipse((bx - 17 * U, by - 17 * U, bx + 17 * U, by + 17 * U), fill=GOLD, outline=INKC, width=3 * U)
            tt = f"{g['count']}"
            d.text((bx - d.textlength(tt, font=mock.font(20 * U)) / 2, by - 15 * U), tt, font=mock.font(20 * U), fill=INKC)
        label = f"{g['average'] * 1000:,.0f} g/s" if "average" in g else "?"
        f = mock.font(17 * U)
        d.text(((x + tw / 2) * U - d.textlength(label, font=f) / 2, (ty + tw + 6) * U), label, font=f, fill=CREAM if "average" in g else (150, 154, 168))
    y = ty + tw + 40
    if len(order) > 6:
        d.text((52 * U, y * U), f"+ {len(order) - 6} more kinds", font=mock.font(17 * U), fill=(160, 164, 176)); y += 28
    text = flavor(w)
    if text and y < 960:
        d.text((52 * U, max(y, 930) * U), mock3.fit_text(d, text, mock.font(19 * U), (CW - 110) * U) if hasattr(mock3, "fit_text") else text[:70], font=mock.font(19 * U), fill=(196, 190, 174))
    foot = f"{SET}  \u00b7  " + (NUMBER(w, index) if NUMBER else f"{index:02d}/{len(WORLDS):02d}")
    d.text((52 * U, 984 * U), foot, font=mock.font(17 * U), fill=(160, 164, 176))
    seed = D["seed"]
    d.text(((CW - 52) * U - d.textlength(seed, font=mock.font(14 * U)), 987 * U), seed, font=mock.font(14 * U), fill=(130, 134, 146))
    return im.resize((CW, CH), Image.LANCZOS)


def rarity2(d, right, cy, w, gs):
    right, cy = right * U, cy * U
    if w["start"]:
        r, pts = 16 * U, []
        for k in range(10):
            a = -math.pi / 2 + k * math.pi / 5
            rr = r if k % 2 == 0 else r * 0.45
            pts.append((right - r + rr * math.cos(a), cy + rr * math.sin(a)))
        d.polygon(pts, fill=GOLD, outline=INKC, width=2 * U)
        return
    n = min(3, len({g["prefab"] for g in gs})) or 1
    for k in range(n):
        x = right - 11 * U - k * 26 * U
        d.polygon([(x, cy - 11 * U), (x + 10 * U, cy), (x, cy + 11 * U), (x - 10 * U, cy)], fill=GOLD, outline=INKC, width=2 * U)


def card_set(out, only=None):
    ws = [w for w in WORLDS if only is None or w["id"] in only]
    cols = min(4, len(ws))
    rows = (len(ws) + cols - 1) // cols
    W, H = 80 + cols * (CW + 30), 240 + rows * (CH + 30) + 40
    canvas = Image.new("RGBA", (W, H), (24, 22, 28, 255))
    canvas.alpha_composite(outlined(headline(D["colony"].upper(), 72), 3), (80, 60))
    from PIL import ImageDraw as _D
    _D.Draw(canvas).text((84, 150), f"{len(WORLDS)} asteroids  ·  Cycle {D['cycle']}  ·  {D['seed']}", font=mock.font(26), fill=(170, 166, 156))
    for i, w in enumerate(ws):
        canvas.alpha_composite(card_full(w, WORLDS.index(w) + 1), (80 + (i % cols) * (CW + 30), 240 + (i // cols) * (CH + 30)))
    canvas.convert("RGB").save(out)
    print(out, canvas.size)


if __name__ == "__main__":
    import sys
    only = None if len(sys.argv) < 4 else {int(x) for x in sys.argv[3].split(",")}
    card_set(os.path.join(OUT, "f-cards.png"), only)
