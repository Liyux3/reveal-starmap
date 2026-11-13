# Rarity tiers on the full-art cards: iridescent foil, engraved filigree, hidden variants.
# usage: uv run --with pillow --with numpy mock5.py RUN_DIR OUT_DIR
import hashlib, math, os, re
import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageChops
import mock, mock4
from mock4 import S, U, CW, CH, INKC, GOLD, D, WORLDS, holo

METALS = {"molten_copper", "molten_iron", "molten_gold", "molten_aluminum", "molten_cobalt", "molten_tungsten", "molten_niobium"}
RANK = ["Common", "Uncommon", "Rare", "Ultra", "Secret"]


def kinds(gs): return {g["prefab"].replace("GeyserGeneric_", "") for g in gs}


def roll(w): return int(hashlib.sha256((D["seed"] + "|" + w["name"]).encode()).hexdigest()[:8], 16) % 16


def tier(w, gs):
    k = kinds(gs)
    metal = k & METALS
    if roll(w) == 0: return "Secret"
    if w["start"] or len(metal) >= 3: return "Ultra"
    if metal or len(k) >= 5: return "Rare"
    if len(k) >= 2 or len(gs) >= 3: return "Uncommon"
    return "Common"


def guilloche(size, seed, colour_fn, density=1.0):
    """Banknote-style engraving: families of hypotrochoid rosettes and wave bands."""
    w, h = size
    layer = Image.new("RGBA", size, (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    rnd = np.random.default_rng(seed)
    t = np.linspace(0, 2 * math.pi * 7, 4000)
    for cx, cy, R in ((w / 2, h / 2, max(w, h) * 0.62), (0, 0, w * 0.32), (w, 0, w * 0.32), (0, h, w * 0.32), (w, h, w * 0.32)):
        for k in range(int(9 * density)):
            r = R * (0.21 + 0.012 * k)
            dd = R * (0.55 + 0.03 * k)
            x = cx + (R - r) * np.cos(t) + dd * np.cos((R - r) / r * t)
            y = cy + (R - r) * np.sin(t) - dd * np.sin((R - r) / r * t)
            pts = list(zip(x.tolist(), y.tolist()))
            d.line(pts, fill=colour_fn(k), width=max(1, U))
    for edge in range(int(14 * density)):   # wave bands that follow the frame
        off = 8 * U + edge * 2.2 * U
        xs = np.linspace(0, w, 600)
        ys = off + 3 * U * np.sin(xs / (11 * U) + edge * 0.6)
        d.line(list(zip(xs.tolist(), ys.tolist())), fill=colour_fn(edge), width=max(1, U // 2))
        d.line(list(zip(xs.tolist(), (h - ys).tolist())), fill=colour_fn(edge), width=max(1, U // 2))
        ys2 = np.linspace(0, h, 800)
        xs2 = off + 3 * U * np.sin(ys2 / (11 * U) + edge * 0.6)
        d.line(list(zip(xs2.tolist(), ys2.tolist())), fill=colour_fn(edge), width=max(1, U // 2))
        d.line(list(zip((w - xs2).tolist(), ys2.tolist())), fill=colour_fn(edge), width=max(1, U // 2))
    return layer


def rainbow(size, seed):
    sheen = holo(size, seed)
    return sheen


def band_mask(size, outer, inner):
    m = Image.new("L", size, 0)
    ImageDraw.Draw(m).polygon(outer, fill=255)
    ImageDraw.Draw(m).polygon(inner, fill=0)
    return m


def rosette(d, cx, cy, r, colour):
    t = np.linspace(0, 2 * math.pi, 720)
    for k in range(5):
        rr = r * (0.55 + 0.09 * k)
        x = cx + rr * np.cos(t) * (1 + 0.18 * np.cos(8 * t + k))
        y = cy + rr * np.sin(t) * (1 + 0.18 * np.cos(8 * t + k))
        d.line(list(zip(x.tolist(), y.tolist())), fill=colour, width=2 * U)
    d.ellipse((cx - r * 0.3, cy - r * 0.3, cx + r * 0.3, cy + r * 0.3), fill=colour, outline=INKC, width=2 * U)


def decorate(im, outer, inner, w, gs):
    t = tier(w, gs)
    size = im.size
    mask = band_mask(size, outer, inner)
    if t in ("Ultra", "Secret"):
        if t == "Secret":   # black-gold body for the hidden variant
            black = Image.new("RGBA", size, (16, 14, 20, 255))
            im.paste(black, (0, 0), mask)
        gold = lambda k: (236, 196, 104, 210) if k % 2 == 0 else (178, 132, 58, 190)
        lines = guilloche(size, w["id"], gold)
        if t == "Secret":
            iri = rainbow(size, w["id"] * 1.7)
            iri.putalpha(Image.fromarray((np.asarray(lines)[..., 3]).astype(np.uint8)))
            lines = Image.alpha_composite(lines, iri)
        im.paste(Image.alpha_composite(im, lines), (0, 0), mask)
        d = ImageDraw.Draw(im)
        for cx, cy in ((30, 30), (CW - 30, 30), (30, CH - 30), (CW - 30, CH - 30)):
            rosette(d, cx * U, cy * U, 22 * U, (240, 204, 112, 255))
        if t == "Secret":   # a faint iridescent veil over the art as well
            veil = rainbow(size, w["id"] * 3.1)
            a = np.asarray(veil)[..., 3] * 0.35
            veil.putalpha(Image.fromarray(a.astype(np.uint8)))
            inner_m = Image.new("L", size, 0)
            ImageDraw.Draw(inner_m).polygon(inner, fill=255)
            im.paste(Image.alpha_composite(im, veil), (0, 0), inner_m)
    if D["cycle"] <= 1:   # first-edition mark, like early printings of a set
        d = ImageDraw.Draw(im)
        cx, cy, r = (CW - 92) * U, 600 * U, 26 * U
        d.ellipse((cx - r, cy - r, cx + r, cy + r), fill=(24, 22, 28, 230), outline=(240, 236, 220), width=3 * U)
        f = mock.font(16 * U)
        d.text((cx - d.textlength("1st", font=f) / 2, cy - 13 * U), "1st", font=f, fill=(240, 236, 220))
    return im


def title(head, w, gs):
    t = tier(w, gs)
    if t not in ("Ultra", "Secret"): return head
    a = np.asarray(head).astype(float)
    hgt = a.shape[0]
    grad = np.linspace(0, 1, hgt)[:, None]
    gold = np.stack([246 - 40 * grad, 214 - 70 * grad, 128 - 70 * grad], -1)[..., :3]
    a[..., :3] = np.broadcast_to(gold, a[..., :3].shape)
    return Image.fromarray(a.astype(np.uint8), "RGBA")


def number(w, index):
    if tier(w, mock4.info(w)[1]) == "Secret":
        secrets = [x for x in WORLDS if tier(x, mock4.info(x)[1]) == "Secret"]
        return f"{len(WORLDS) + 1 + secrets.index(w):02d}/{len(WORLDS):02d}  SECRET"
    return f"{index:02d}/{len(WORLDS):02d}  {tier(w, mock4.info(w)[1]).upper()}"


def holo_rule(w, gs): return tier(w, gs) in ("Rare", "Ultra", "Secret")


mock4.DECORATE, mock4.TITLE, mock4.NUMBER = decorate, title, number
mock4.HOLO = holo_rule
_rarity2 = mock4.rarity2


def rarity_mark(d, right, cy, w, gs):
    t = tier(w, gs)
    n = RANK.index(t) + 1
    rx, ry = right * U, cy * U
    for k in range(n):
        x = rx - 11 * U - k * 26 * U
        if t in ("Ultra", "Secret"):
            r, pts = 13 * U, []
            for j in range(10):
                a = -math.pi / 2 + j * math.pi / 5
                rr = r if j % 2 == 0 else r * 0.45
                pts.append((x + rr * math.cos(a), ry + rr * math.sin(a)))
            d.polygon(pts, fill=GOLD, outline=INKC, width=2 * U)
        else:
            d.polygon([(x, ry - 11 * U), (x + 10 * U, ry), (x, ry + 11 * U), (x - 10 * U, ry)], fill=GOLD if t != "Common" else (200, 200, 210), outline=INKC, width=2 * U)


mock4.rarity2 = rarity_mark

if __name__ == "__main__":
    for w in WORLDS: print(w["name"], tier(w, mock4.info(w)[1]), roll(w))
    mock4.card_set(os.path.join(mock4.OUT, "g-cards.png"))
