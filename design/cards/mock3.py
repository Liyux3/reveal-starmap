# Collectible treatments: a trading card per asteroid and a stamp souvenir sheet for the cluster.
# usage: uv run --with pillow --with numpy mock3.py RUN_DIR OUT_DIR
import os, re, random
from PIL import Image, ImageDraw, ImageFilter
import mock, mock2
from mock2 import sprite, headline, INK, MUTE, PANEL, PANEL_EDGE, NAMES

OUT = mock2.OUT
D = mock.D
WORLDS = sorted(D["worlds"], key=lambda w: (not w["start"], -w["w"] * w["h"]))
SET = re.sub(r"^[A-Z]+-([A-Z]+-[A-Z]).*$", r"\1", D["seed"])  # e.g. SNDST-C
CREAM, PAPER, NAVY = (239, 230, 210), (251, 248, 240), (36, 40, 58)
GOLD = (232, 196, 120)


def info(w):
    elem, zone = mock.cells(w)
    t, zone, state = mock.terrain(w, mock.DARK)
    shares = mock.biome_share(zone, state)
    # Analyzed first, then identified pending spawns, then generic ones whose type is still open.
    gs = sorted(mock.geysers(w), key=lambda g: ("average" not in g, g["prefab"] == "GeyserGeneric", g["label"]))
    known = {s["id"]: s for s in mock2.A["saved"]}
    traits = w.get("traits") or known.get(f"world-{w['id']}", {}).get("traits") or []
    return shares, gs, traits


def rarity(d, right, cy, w, gs, colour=GOLD):
    """Drawn marks, since the game font has no star glyphs: a star for the start asteroid,
    one diamond per geyser up to three, a dot for none."""
    import math
    if w["start"]:
        r, pts = 13, []
        for k in range(10):
            a = -math.pi / 2 + k * math.pi / 5
            rr = r if k % 2 == 0 else r * 0.45
            pts.append((right - r + rr * math.cos(a), cy + rr * math.sin(a)))
        d.polygon(pts, fill=colour)
        return
    n = min(3, len(gs))
    if n == 0:
        d.ellipse((right - 10, cy - 5, right, cy + 5), fill=colour)
    for k in range(n):
        x = right - 9 - k * 22
        d.polygon([(x, cy - 9), (x + 8, cy), (x, cy + 9), (x - 8, cy)], fill=colour)


def starfield(size, seed, base=(14, 16, 26)):
    im = Image.new("RGBA", size, base + (255,))
    d = ImageDraw.Draw(im)
    rnd = random.Random(seed)
    for _ in range(size[0] * size[1] // 900):
        x, y, r = rnd.randrange(size[0]), rnd.randrange(size[1]), rnd.choice((1, 1, 1, 2))
        a = rnd.randrange(90, 230)
        d.ellipse((x - r, y - r, x + r, y + r), fill=(235, 236, 255, a))
    return im


def planet(w, size):
    p = sprite(f"world-{w['id']}", 512)
    if p is None: return None
    s = size / max(p.width, p.height)
    return p.resize((round(p.width * s), round(p.height * s)), Image.LANCZOS)


def fit(im, width):
    return im if im.width <= width else im.resize((width, round(im.height * width / im.width)), Image.LANCZOS)


def geyser_icon(prefab, size):
    if prefab == "GeyserGeneric":
        icon = sprite("GeyserGeneric_steam", size)
        return mock2.tint(icon, (112, 117, 130)) if icon else None
    return sprite(prefab, size)


def flavor(w):
    text = re.sub(r"<[^>]+>", "", (w.get("description") or "").split("\n\n")[0]).strip()
    return text


def card(w, index):
    CW, CH = 630, 882
    shares, gs, traits = info(w)
    lead = shares[0][1] if shares else (120, 120, 140)
    frame = mock.mix(lead, (20, 22, 30), 0.25)
    im = Image.new("RGBA", (CW, CH), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    d.rounded_rectangle((0, 0, CW - 1, CH - 1), 30, fill=frame + (255,))
    d.rounded_rectangle((16, 16, CW - 17, CH - 17), 20, fill=PANEL + (255,))
    # Name bar with size, like a card's title and hit points.
    d.rounded_rectangle((28, 28, CW - 29, 92), 14, fill=mock.mix(lead, PANEL, 0.55) + (255,))
    name = fit(headline(w["name"].upper(), 40), CW - 230)
    im.alpha_composite(name, (46, 60 - name.height // 2))
    size_text = f"{w['w']}×{w['h']}"
    f = mock.font(24)
    d.text((CW - 46 - d.textlength(size_text, font=f), 45), size_text, font=f, fill=INK)
    # Art window: the asteroid's own starmap art on a starfield.
    art = (36, 104, CW - 37, 440)
    sky = starfield((art[2] - art[0], art[3] - art[1]), w["id"])
    glow = Image.new("RGBA", sky.size, (0, 0, 0, 0))
    ImageDraw.Draw(glow).ellipse((sky.width / 2 - 150, sky.height / 2 - 150, sky.width / 2 + 150, sky.height / 2 + 150), fill=lead + (90,))
    sky.alpha_composite(glow.filter(ImageFilter.GaussianBlur(50)))
    p = planet(w, 290)
    if p: sky.alpha_composite(p, ((sky.width - p.width) // 2, (sky.height - p.height) // 2))
    mask = Image.new("L", sky.size, 0)
    ImageDraw.Draw(mask).rounded_rectangle((0, 0, sky.width - 1, sky.height - 1), 12, fill=255)
    im.paste(sky, art[:2], mask)
    d.rounded_rectangle(art, 12, outline=mock.mix(lead, (255, 255, 255), 0.25), width=3)
    # Type line and rarity.
    kind = "Start Asteroid" if w["start"] else (" · ".join(traits[:2]) or "Asteroid")
    d.rounded_rectangle((28, 452, CW - 29, 494), 10, fill=mock.mix(lead, PANEL, 0.75) + (255,))
    d.text((46, 460), kind, font=mock.font(21), fill=INK)
    rarity(d, CW - 46, 473, w, gs)
    # Geysers as the card's moves: icon, name, output on the right.
    y = 510
    # Identical unanalyzed geysers collapse into one row with a count, like duplicate cards.
    rows, seen = [], {}
    for g in gs:
        if "average" in g: rows.append(dict(g, count=1)); continue
        if g["prefab"] in seen: seen[g["prefab"]]["count"] += 1; continue
        seen[g["prefab"]] = dict(g, count=1); rows.append(seen[g["prefab"]])
    extra = max(0, len(rows) - 3) if len(rows) > 3 else 0
    rows = rows[:3] if extra else rows
    if not rows:
        d.text((48, y + 12), "No geysers found on this asteroid.", font=mock.font(20), fill=MUTE)
    for g in rows:
        icon = geyser_icon(g["prefab"], 58)
        if icon: im.alpha_composite(icon, (44 + (58 - icon.width) // 2, y + (58 - icon.height) // 2))
        label = re.sub(r"<[^>]+>", "", NAMES.get(g["prefab"], g["label"])) if g["prefab"] != "GeyserGeneric" else g["label"]
        d.text((116, y + 4), label, font=mock.font(22), fill=INK)
        if g["count"] > 1:
            tx = 116 + d.textlength(label, font=mock.font(22)) + 10
            d.rounded_rectangle((tx, y + 6, tx + 44, y + 32), 13, fill=(58, 62, 78))
            d.text((tx + 8, y + 7), f"\u00d7{g['count']}", font=mock.font(18), fill=GOLD)
        if "average" in g:
            d.text((116, y + 32), f"{g['temperature'] - 273.15:,.0f} °C  ·  active {g['yearOn'] / g['year'] * 100:.0f}% of the time", font=mock.font(16), fill=MUTE)
            big = f"{g['average'] * 1000:,.0f}"
            d.text((CW - 92 - d.textlength(big, font=mock.font(34)), y + 6), big, font=mock.font(34), fill=INK)
            d.text((CW - 84, y + 22), "g/s", font=mock.font(16), fill=MUTE)
        else:
            d.text((116, y + 32), "Uncovered when explored", font=mock.font(16), fill=MUTE)
            d.text((CW - 70, y + 6), "?", font=mock.font(34), fill=MUTE)
        y += 66
    if extra:
        d.text((116, y + 2), f"+ {extra} more kinds", font=mock.font(18), fill=MUTE)
        y += 30
    text = flavor(w)
    if text and y < CH - 200:
        words, line, lines = text.split(), "", []
        for word in words:
            trial = (line + " " + word).strip()
            if d.textlength(trial, font=mock.font(18)) > CW - 96: lines.append(line); line = word
            else: line = trial
        lines.append(line)
        for k, ln in enumerate(lines[:2]):
            d.text((48, CH - 196 + k * 26), ln, font=mock.font(18), fill=(150, 154, 166))
    # Biome strip, then the collector line.
    y = CH - 128
    d.line((40, y - 10, CW - 40, y - 10), fill=PANEL_EDGE, width=2)
    x = 40
    for nm, col, share in shares[:3]:
        label = f"{mock.PRETTY.get(nm, nm)} {share * 100:.0f}%"
        wdt = d.textlength(label, font=mock.font(17)) + 30
        chip = mock.mix(col, PANEL, 0.3)
        dark = 0.299 * chip[0] + 0.587 * chip[1] + 0.114 * chip[2] < 110
        d.rounded_rectangle((x, y, x + wdt, y + 32), 16, fill=chip + (255,))
        d.text((x + 15, y + 5), label, font=mock.font(17), fill=INK if dark else (20, 22, 28))
        x += wdt + 8
    foot = f"{SET}  {index:02d}/{len(WORLDS):02d}"
    d.text((40, CH - 66), foot, font=mock.font(17), fill=MUTE)
    d.text((CW - 40 - d.textlength(D["seed"], font=mock.font(14)), CH - 63), D["seed"], font=mock.font(14), fill=(110, 114, 126))
    return im


def card_set(out):
    cols = 4
    rows = (len(WORLDS) + cols - 1) // cols
    W, H = 80 + cols * 662, 230 + rows * 914 + 40
    canvas = Image.new("RGBA", (W, H), (21, 23, 29, 255))
    canvas.alpha_composite(headline(D["colony"].upper(), 70), (80, 56))
    ImageDraw.Draw(canvas).text((82, 146), f"{len(WORLDS)} asteroids  ·  Cycle {D['cycle']}  ·  {D['seed']}", font=mock.font(26), fill=MUTE)
    for i, w in enumerate(WORLDS):
        canvas.alpha_composite(card(w, i + 1), (80 + (i % cols) * 662, 230 + (i // cols) * 914))
    canvas.convert("RGB").save(out)
    print(out, canvas.size)


def perforate(im, step=22, r=7, bg=CREAM):
    d = ImageDraw.Draw(im)
    W, H = im.size
    for x in range(step // 2, W, step):
        for y in (0, H):
            d.ellipse((x - r, y - r, x + r, y + r), fill=bg + (255,))
    for y in range(step // 2, H, step):
        for x in (0, W):
            d.ellipse((x - r, y - r, x + r, y + r), fill=bg + (255,))
    return im


def stamp(w, size):
    SW, SH = size
    shares, gs, traits = info(w)
    lead = shares[0][1] if shares else (150, 150, 160)
    im = Image.new("RGBA", (SW, SH), PAPER + (255,))
    d = ImageDraw.Draw(im)
    inner = (24, 24, SW - 25, SH - 25)
    field = mock.mix(lead, PAPER, 0.35)
    d.rectangle(inner, fill=field + (255,))
    # Issuer at the top, value at the corner, asteroid in the middle, name below.
    issuer = fit(headline(D["colony"].upper(), 22), SW - 120)
    tinted = mock2.tint(issuer, NAVY)
    im.alpha_composite(tinted, (SW - 40 - tinted.width, 40))
    d.text((40, 30), str(len(gs)), font=mock.font(round(SH * 0.11)), fill=NAVY)
    d.text((42, 30 + round(SH * 0.13)), "GEYSERS", font=mock.font(13), fill=NAVY)
    p = planet(w, round(min(SW, SH) * 0.58))
    if p: im.alpha_composite(p, ((SW - p.width) // 2, round(SH * 0.48 - p.height / 2)))
    nm = mock2.tint(fit(headline(w["name"].upper(), round(SH * 0.07)), SW - 80), NAVY)
    im.alpha_composite(nm, ((SW - nm.width) // 2, SH - 40 - nm.height - 22))
    sub = f"{w['w']} × {w['h']}"
    d.text(((SW - d.textlength(sub, font=mock.font(14))) / 2, SH - 54), sub, font=mock.font(14), fill=NAVY)
    return perforate(im)


def postmark(canvas, center, radius, lines):
    layer = Image.new("RGBA", canvas.size, (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    ink = NAVY + (150,)
    cx, cy = center
    d.ellipse((cx - radius, cy - radius, cx + radius, cy + radius), outline=ink, width=5)
    d.ellipse((cx - radius + 14, cy - radius + 14, cx + radius - 14, cy + radius - 14), outline=ink, width=2)
    for k, (text, px) in enumerate(lines):
        f = mock.font(px)
        y = cy - radius * 0.55 + k * radius * 0.42
        d.text((cx - d.textlength(text, font=f) / 2, y), text, font=f, fill=ink)
    for k in range(5):  # the wavy killer bars
        y = cy - 40 + k * 20
        pts = [(cx + radius + 10 + t, y + 6 * ((t // 20) % 2)) for t in range(0, 360, 20)]
        d.line(pts, fill=ink, width=4)
    canvas.alpha_composite(layer.rotate(-12, center=center, resample=Image.BICUBIC))


def sheet(out):
    unit = (300, 380)
    gap = 26
    big = (unit[0] * 2 + gap, unit[1] * 2 + gap)
    cols = 4
    others = [w for w in WORLDS if not w["start"]]
    start = next(w for w in WORLDS if w["start"])
    W = 120 + cols * unit[0] + (cols - 1) * gap + 120
    rows = 2 + (max(0, len(others) - 4) + cols - 1) // cols
    H = 260 + rows * (unit[1] + gap) + 140
    canvas = Image.new("RGBA", (W, H), CREAM + (255,))
    d = ImageDraw.Draw(canvas)
    d.rectangle((40, 40, W - 41, H - 41), outline=NAVY + (255,), width=2)
    title = mock2.tint(headline(D["colony"].upper(), 76), NAVY)
    canvas.alpha_composite(title, ((W - title.width) // 2, 80))
    sub = f"{len(WORLDS)} asteroids  ·  {D['seed']}"
    d.text(((W - d.textlength(sub, font=mock.font(24))) / 2, 176), sub, font=mock.font(24), fill=NAVY)
    x0, y0 = 120, 260
    canvas.alpha_composite(stamp(start, big), (x0, y0))
    slots = [(x0 + 2 * (unit[0] + gap) + c * (unit[0] + gap), y0 + r * (unit[1] + gap)) for r in range(2) for c in range(2)]
    slots += [(x0 + c * (unit[0] + gap), y0 + (2 + r) * (unit[1] + gap)) for r in range(rows - 2) for c in range(cols)]
    for w, at in zip(others, slots):
        canvas.alpha_composite(stamp(w, unit), at)
    postmark(canvas, (x0 + big[0] - 40, y0 + big[1] - 30), 110, [(D["colony"].upper(), 22), (f"CYCLE {D['cycle']}", 30), (SET, 20)])
    foot = "Reveal StarMap"
    d.text((W - 60 - d.textlength(foot, font=mock.font(18)), H - 80), foot, font=mock.font(18), fill=NAVY)
    canvas.convert("RGB").save(out)
    print(out, canvas.size)


if __name__ == "__main__":
    card_set(os.path.join(OUT, "d-cards.png"))
    sheet(os.path.join(OUT, "e-stamps.png"))
