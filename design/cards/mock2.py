# Game-styled export mockups from a LiveProbe run (dump, assets, render exports).
# usage: uv run --with pillow --with numpy mock2.py RUN_DIR OUT_DIR
import json, os, sys, glob, re
from PIL import Image, ImageDraw, ImageFont, ImageEnhance, ImageFilter
import mock  # reuses terrain colouring, geyser table and helpers

RUN, OUT = sys.argv[1], sys.argv[2]
os.makedirs(OUT, exist_ok=True)
A = json.load(open(os.path.join(RUN, "assets", "assets.json")))
TEXTS = A["texts"]
PANEL, PANEL_EDGE, HEADER, ACCENT = (31, 34, 43), (74, 79, 102), (43, 47, 58), (138, 75, 110)
INK, MUTE = (240, 238, 232), (166, 170, 182)
NAMES = {s["id"]: s["name"] for s in A["saved"]}


def sprite(prefab, size):
    p = os.path.join(RUN, "assets", prefab + ".png")
    if not os.path.exists(p): return None
    im = Image.open(p).convert("RGBA")
    im.thumbnail((size, size), Image.LANCZOS)
    return im


def headline(text, height, face=2):
    """Game header face rendered by the probe; falls back to the body font."""
    if text in TEXTS:
        p = os.path.join(RUN, "assets", f"text-{face}-{TEXTS.index(text)}.png")
        if os.path.exists(p):
            im = Image.open(p).convert("RGBA")
            bbox = im.getbbox()
            im = im.crop(bbox)
            return im.resize((round(im.width * height / im.height), height), Image.LANCZOS)
    f = mock.font(height)
    im = Image.new("RGBA", (int(f.getlength(text)) + 4, int(height * 1.3)), (0, 0, 0, 0))
    ImageDraw.Draw(im).text((0, 0), text, font=f, fill=INK)
    return im.crop(im.getbbox())


def tint(im, rgb):
    r, g, b, a = im.split()
    solid = Image.new("RGBA", im.size, rgb + (255,))
    solid.putalpha(a)
    return solid


def panel(d, box, radius=10, header=None):
    d.rounded_rectangle(box, radius, fill=PANEL + (238,), outline=PANEL_EDGE + (255,), width=2)
    if header:
        x0, y0, x1, _ = box
        d.rounded_rectangle((x0, y0, x1, y0 + header), radius, fill=HEADER + (255,))
        d.rectangle((x0, y0 + header - radius, x1, y0 + header), fill=HEADER + (255,))


def render_map(world):
    """The asteroid as the game draws it, cropped from the probe's render export."""
    hits = [p for p in glob.glob(os.path.join(RUN, "exports", "*.png")) if world["name"] in os.path.basename(p)]
    if not hits: return None
    im = Image.open(hits[0]).convert("RGB")
    w, h = world["w"], world["h"]
    px = (4 * im.width - 2 * im.height) / (4 * w - 2 * h)
    m = round((im.width - w * px) / 2)
    return im.crop((m, m, m + round(w * px), m + round(h * px)))


def expedition(world, out):
    base = render_map(world)
    if base is None: print("no render for", world["name"]); return
    scale = 1500 / base.height
    base = base.resize((round(base.width * scale), 1500), Image.LANCZOS)
    base = ImageEnhance.Brightness(ImageEnhance.Color(base).enhance(0.8)).enhance(0.72)
    px = base.width / world["w"]
    gs = mock.geysers(world)
    col_w, gap = 430, 56
    W, H = base.width + 2 * (col_w + 2 * gap), 1500 + 260
    canvas = Image.new("RGBA", (W, H), (21, 23, 29, 255))
    mx, my = col_w + 2 * gap, 200
    canvas.paste(base, (mx, my))
    d = ImageDraw.Draw(canvas)
    d.rounded_rectangle((mx - 3, my - 3, mx + base.width + 3, my + 1503), 8, outline=PANEL_EDGE, width=3)
    # Header plaque: planet art, name in the game's header face, stats.
    planet = sprite(f"world-{world['id']}", 150)
    if planet: canvas.alpha_composite(planet, (mx - planet.width // 2 + 20, 24))
    title = headline(world["name"].upper(), 64)
    canvas.alpha_composite(title, (mx + 130, 44))
    d.text((mx + 132, 124), f"{world['w']} × {world['h']}  ·  Cycle {mock.D['cycle']}  ·  {mock.D['seed']}", font=mock.font(24), fill=MUTE)
    # Callout cards in two columns, each joined to its geyser by a leader.
    left = [g for g in gs if g["x"] < world["w"] / 2]
    right = [g for g in gs if g["x"] >= world["w"] / 2]
    for side, items in ((0, left), (1, right)):
        items.sort(key=lambda g: -g["y"])
        step = min(170, 1500 / max(1, len(items)))
        for k, g in enumerate(items):
            gx, gy = mx + (g["x"] + 1) * px, my + (world["h"] - g["y"] - 1) * px
            cy = my + 20 + k * step
            cx0 = gap if side == 0 else mx + base.width + gap
            box = (cx0, cy, cx0 + col_w, cy + 132)
            anchor = (box[2], cy + 66) if side == 0 else (box[0], cy + 66)
            d.line([anchor, ((anchor[0] + gx) / 2, anchor[1]), (gx, gy)], fill=(230, 226, 214, 200), width=3)
            d.ellipse((gx - 12, gy - 12, gx + 12, gy + 12), outline=(240, 238, 232), width=4)
            panel(d, box)
            icon = sprite(g["prefab"], 96)
            if icon: canvas.alpha_composite(icon, (box[0] + 18 + (96 - icon.width) // 2, cy + 18 + (96 - icon.height) // 2))
            name = NAMES.get(g["prefab"], g["label"])
            d.text((box[0] + 132, cy + 22), re.sub(r"<[^>]+>", "", name), font=mock.font(27), fill=INK)
            if "average" in g:
                d.text((box[0] + 132, cy + 62), f"{g['average'] * 1000:,.0f} g/s average", font=mock.font(22), fill=(232, 196, 120))
                d.text((box[0] + 132, cy + 92), f"{g['temperature'] - 273.15:,.0f} °C  ·  active {g['yearOn'] / g['year'] * 100:.0f}%", font=mock.font(20), fill=MUTE)
            else:
                d.text((box[0] + 132, cy + 64), "Not yet analyzed", font=mock.font(22), fill=MUTE)
    pod = [l for l in mock.landmarks(world)]
    for l in pod:
        gx, gy = mx + (l["x"] + 1) * px, my + (world["h"] - l["y"] - 2) * px
        icon = sprite("Headquarters", 72)
        if icon: canvas.alpha_composite(icon, (round(gx - icon.width / 2), round(gy - icon.height / 2)))
    credit = "Reveal StarMap"
    d.text((W - gap - d.textlength(credit, font=mock.font(20)), H - 52), credit, font=mock.font(20), fill=(110, 114, 126))
    canvas.convert("RGB").save(out)
    print(out, canvas.size)


def card(world, theme=mock.DARK):
    """One collectible card per asteroid."""
    CW, CH = 520, 860
    im = Image.new("RGBA", (CW, CH), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    d.rounded_rectangle((0, 0, CW - 1, CH - 1), 22, fill=PANEL + (255,), outline=PANEL_EDGE, width=3)
    d.rounded_rectangle((0, 0, CW - 1, 300), 22, fill=HEADER + (255,))
    d.rectangle((0, 280, CW - 1, 300), fill=HEADER + (255,))
    planet = sprite(f"world-{world['id']}", 230)
    if planet: im.alpha_composite(planet, ((CW - planet.width) // 2, 34))
    title = headline(world["name"].upper(), 44)
    if title.width > CW - 60: title = title.resize((CW - 60, round(title.height * (CW - 60) / title.width)), Image.LANCZOS)
    im.alpha_composite(title, ((CW - title.width) // 2, 318))
    role = "START ASTEROID" if world["start"] else f"{world['w']} × {world['h']}"
    f = mock.font(20)
    d.text(((CW - d.textlength(role, font=f)) / 2, 378), role, font=f, fill=(232, 196, 120))
    # Mini terrain in the information style, framed like an in-game thumbnail.
    t, zone, state = mock.terrain(world, theme)
    thumb = Image.fromarray(t)
    thumb.thumbnail((CW - 80, 200), Image.NEAREST)
    tx = (CW - thumb.width) // 2
    im.paste(thumb, (tx, 420))
    d.rounded_rectangle((tx - 3, 417, tx + thumb.width + 3, 423 + thumb.height), 6, outline=PANEL_EDGE, width=2)
    y = 440 + thumb.height
    shares = mock.biome_share(zone, state)[:4]
    x = 36
    for name, colour, share in shares:
        label = mock.PRETTY.get(name, name)
        wdt = d.textlength(label, font=mock.font(17)) + 34
        if x + wdt > CW - 30: break
        d.rounded_rectangle((x, y, x + wdt, y + 30), 15, fill=mock.mix(colour, PANEL, 0.35))
        d.text((x + 17, y + 4), label, font=mock.font(17), fill=(20, 22, 28))
        x += wdt + 8
    y += 50
    for g in mock.geysers(world)[:5]:
        icon = sprite(g["prefab"], 44)
        if icon: im.alpha_composite(icon, (36 + (44 - icon.width) // 2, y + (44 - icon.height) // 2))
        d.text((92, y + 2), re.sub(r"<[^>]+>", "", NAMES.get(g["prefab"], g["label"])), font=mock.font(20), fill=INK)
        sub = f"{g['average'] * 1000:,.0f} g/s" if "average" in g else "not yet analyzed"
        d.text((92, y + 26), sub, font=mock.font(16), fill=MUTE)
        y += 54
    seed = mock.D["seed"]
    d.text(((CW - d.textlength(seed, font=mock.font(15))) / 2, CH - 40), seed, font=mock.font(15), fill=(110, 114, 126))
    return im


def cards(out):
    ws = sorted(mock.D["worlds"], key=lambda w: (not w["start"], -w["w"] * w["h"]))
    cols = 4
    rows = (len(ws) + cols - 1) // cols
    W, H = 64 + cols * (520 + 32), 210 + rows * (860 + 32) + 40
    canvas = Image.new("RGBA", (W, H), (21, 23, 29, 255))
    title = headline(mock.D["colony"].upper(), 64)
    canvas.alpha_composite(title, (64, 50))
    ImageDraw.Draw(canvas).text((66, 130), f"{len(ws)} asteroids  ·  Cycle {mock.D['cycle']}  ·  {mock.D['seed']}", font=mock.font(24), fill=MUTE)
    for i, w in enumerate(ws):
        canvas.alpha_composite(card(w), (64 + (i % cols) * 552, 210 + (i // cols) * 892))
    canvas.convert("RGB").save(out)
    print(out, canvas.size)


if __name__ == "__main__":
    start = next(w for w in mock.D["worlds"] if w["start"])
    expedition(start, os.path.join(OUT, "a-expedition.png"))
    cards(os.path.join(OUT, "c-cards.png"))
