# Full collectible vocabulary: finishes, ink stamps and seals, several hidden variants.
# usage: uv run --with pillow --with numpy mock6.py RUN_DIR OUT_DIR
import hashlib, math, os, random
import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageOps, ImageChops
import mock, mock4, mock5
from mock4 import S, U, CW, CH, INKC, GOLD, D, WORLDS, holo, sprite
from mock5 import guilloche, band_mask, rosette, kinds, METALS

FORCE = None
HIDDEN = {"Aurora", "Golden", "Shadow", "Misprint"}


def h32(w): return int(hashlib.sha256((D["seed"] + "|" + w["name"]).encode()).hexdigest()[:8], 16)


def finish(w, gs):
    if FORCE: return FORCE
    h = h32(w)
    if h % 100 == 2: return "Misprint"
    if h % 64 == 3: return "Golden"
    if h % 48 == 5: return "Shadow"
    if h % 16 == 0: return "Aurora"
    t = mock5.tier(w, gs)
    if t == "Ultra": return "Gilded"
    if t == "Rare": return "Holo"
    shares = mock4.info(w)[0]
    lead, share = (shares[0][0], shares[0][2]) if shares else ("", 0)
    if share >= 0.6:
        if lead in ("FrozenWastes", "IceCaves"): return "Frost"
        if lead in ("MagmaCore",): return "Molten"
        if lead in ("Ocean",): return "Tidal"
    if t == "Uncommon": return "Etched"
    return "Plain"


def sparkles(size, seed, n, colour_fn):
    layer = Image.new("RGBA", size, (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    rnd = random.Random(seed)
    for i in range(n):
        x, y, r = rnd.randrange(size[0]), rnd.randrange(size[1]), rnd.choice((3, 4, 6, 9)) * U
        c = colour_fn(rnd.random())
        d.polygon([(x, y - r), (x + r * 0.25, y - r * 0.25), (x + r, y), (x + r * 0.25, y + r * 0.25), (x, y + r),
                   (x - r * 0.25, y + r * 0.25), (x - r, y), (x - r * 0.25, y - r * 0.25)], fill=c)
    return layer


def cracks(size, seed, colour, n=40, step=9):
    layer = Image.new("RGBA", size, (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    rnd = random.Random(seed)
    for _ in range(n):
        x, y = rnd.randrange(size[0]), rnd.randrange(size[1])
        a = rnd.random() * 6.28
        pts = [(x, y)]
        for _ in range(rnd.randrange(6, 16)):
            a += rnd.uniform(-0.7, 0.7)
            x += math.cos(a) * step * U; y += math.sin(a) * step * U
            pts.append((x, y))
        d.line(pts, fill=colour, width=2 * U)
    return layer


def waves(size, colour):
    layer = Image.new("RGBA", size, (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    for k in range(0, size[1], 9 * U):
        xs = np.linspace(0, size[0], 400)
        ys = k + 4 * U * np.sin(xs / (14 * U) + k * 0.05)
        d.line(list(zip(xs.tolist(), ys.tolist())), fill=colour, width=2 * U)
    return layer


def rainbow_fn(alpha):
    def f(t):
        r, g, b = [int(127 + 127 * math.sin(6.28 * t + o)) for o in (0, 2.1, 4.2)]
        return (r, g, b, alpha)
    return f


def decorate(im, outer, inner, w, gs):
    f = finish(w, gs)
    size = im.size
    mask = band_mask(size, outer, inner)
    def over(layer, m=mask):
        im.paste(Image.alpha_composite(im, layer), (0, 0), m)
    base = {"Aurora": (16, 14, 22), "Shadow": (16, 14, 22), "Etched": (168, 174, 186), "Frost": (176, 214, 232),
            "Molten": (34, 26, 24), "Tidal": (28, 96, 118)}.get(f)
    if base:   # themed finishes repaint the frame itself, so they read at a glance
        im.paste(mock4.grain(Image.new("RGBA", size, base + (255,)), 8, w["id"]), (0, 0), mask)
    if f == "Etched":
        over(guilloche(size, w["id"], lambda k: (214, 218, 228, 150) if k % 2 == 0 else (150, 156, 170, 130), 0.7))
    if f == "Gilded" or f == "Golden":
        over(guilloche(size, w["id"], lambda k: (236, 196, 104, 215) if k % 2 == 0 else (178, 132, 58, 190)))
    if f == "Aurora":
        lines = guilloche(size, w["id"], lambda k: (255, 255, 255, 230))
        iri = holo(size, w["id"] * 1.7)
        iri.putalpha(Image.fromarray(np.asarray(lines)[..., 3]))
        over(iri)
        over(sparkles(size, w["id"], 90, rainbow_fn(230)))
    if f == "Shadow":
        over(guilloche(size, w["id"], lambda k: (90, 255, 220, 200) if k % 2 == 0 else (190, 90, 255, 200), 0.8))
    if f == "Frost":
        over(cracks(size, w["id"], (255, 255, 255, 235), 120, 8))
        over(sparkles(size, w["id"] + 9, 40, lambda t: (240, 250, 255, 220)))
    if f == "Molten":
        glow = Image.new("RGBA", size, (255, 120, 40, 0))
        a = np.zeros((size[1], size[0]), np.uint8)
        glow.putalpha(Image.fromarray(a))
        over(cracks(size, w["id"], (255, 110, 30, 255), 110, 10).filter(ImageFilter.GaussianBlur(3 * U)))
        over(cracks(size, w["id"], (255, 226, 140, 255), 110, 10))
    if f == "Tidal":
        over(waves(size, (230, 248, 255, 210)))
    if f in ("Gilded", "Golden", "Aurora"):
        d = ImageDraw.Draw(im)
        for cx, cy in ((34, 34), (CW - 34, 34), (34, CH - 34), (CW - 34, CH - 34)):
            rosette(d, cx * U, cy * U, 24 * U, (240, 204, 112, 255))
    if f in ("Holo", "Aurora", "Golden"):
        over(sparkles(size, w["id"] + 3, 50, rainbow_fn(200)))
    return im


def title(head, w, gs):
    f = finish(w, gs)
    a = np.asarray(head).astype(float)
    g = np.linspace(0, 1, a.shape[0])[:, None]
    if f in ("Gilded", "Aurora", "Golden"):
        col = np.stack([246 - 40 * g, 214 - 70 * g, 128 - 70 * g], -1)
    elif f == "Shadow":
        col = np.stack([120 + 0 * g, 255 - 60 * g, 230 - 20 * g], -1)
    elif f == "Etched":
        col = np.stack([236 - 50 * g, 238 - 50 * g, 244 - 40 * g], -1)
    else:
        return head
    a[..., :3] = np.broadcast_to(col, a[..., :3].shape)
    return Image.fromarray(a.astype(np.uint8), "RGBA")


def serial(w): return f"No. {h32(w) % 9999 + 1:04d}"


def number(w, index):
    f = finish(w, mock4.info(w)[1])
    label = {"Plain": "COMMON", "Etched": "UNCOMMON", "Frost": "UNCOMMON", "Molten": "UNCOMMON", "Tidal": "UNCOMMON",
             "Holo": "RARE", "Gilded": "ULTRA RARE", "Aurora": "SECRET", "Golden": "GOLD SECRET",
             "Shadow": "SHADOW SECRET", "Misprint": "MISPRINT"}[f]
    if f in HIDDEN and not FORCE:
        hidden = [x for x in WORLDS if finish(x, mock4.info(x)[1]) in HIDDEN]
        return f"{len(WORLDS) + 1 + hidden.index(w):02d}/{len(WORLDS):02d}  {label}"
    return f"{index:02d}/{len(WORLDS):02d}  {label}"


# Ink stamps and seals, applied on the finished card.
def ink_stamp(card, text, at, colour, angle, seed, size=30, paper=False):
    f = mock.font(size)
    tmp = Image.new("RGBA", (int(f.getlength(text)) + 40, size + 34), (0, 0, 0, 0))
    d = ImageDraw.Draw(tmp)
    if paper:   # a taped paper label, so dark ink reads on any terrain
        d.rectangle((0, 0, tmp.width - 1, tmp.height - 1), fill=(240, 232, 206, 255))
    d.rounded_rectangle((3, 3, tmp.width - 4, tmp.height - 4), 6, outline=colour + (255,), width=4)
    d.text((20, 13), text, font=f, fill=colour + (255,), stroke_width=1, stroke_fill=colour + (255,))
    rng = np.random.default_rng(seed)
    a = np.asarray(tmp.split()[-1]).astype(float)
    a *= np.where(a > 0, (rng.random(a.shape) > 0.1) * 0.92, 0)
    tmp.putalpha(Image.fromarray(a.astype(np.uint8)).filter(ImageFilter.GaussianBlur(0.6)))
    tmp = tmp.rotate(angle, expand=True, resample=Image.BICUBIC)
    card.alpha_composite(tmp, (int(at[0] - tmp.width / 2), int(at[1] - tmp.height / 2)))


def wax_seal(card, at, r=48, seed=0):
    layer = Image.new("RGBA", (r * 3, r * 3), (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    rnd = random.Random(seed)
    c = r * 1.5
    pts = [(c + (r + rnd.uniform(-5, 7)) * math.cos(a), c + (r + rnd.uniform(-5, 7)) * math.sin(a))
           for a in np.linspace(0, 6.28, 40, endpoint=False)]
    d.polygon(pts, fill=(150, 26, 34, 255))
    d.ellipse((c - r * 0.72, c - r * 0.72, c + r * 0.72, c + r * 0.72), fill=(122, 18, 26, 255), outline=(186, 60, 64, 255), width=3)
    pod = sprite("Headquarters", int(r * 1.1))
    if pod:
        g = ImageOps.grayscale(pod.convert("RGB"))
        emb = Image.new("RGBA", pod.size, (90, 10, 18, 0))
        emb.putalpha(ImageChops.multiply(pod.split()[-1], g.point(lambda v: 120 + v // 3)))
        layer.alpha_composite(emb, (int(c - pod.width / 2), int(c - pod.height / 2)))
    hl = Image.new("RGBA", layer.size, (0, 0, 0, 0))
    ImageDraw.Draw(hl).ellipse((c - r * 0.8, c - r * 0.9, c + r * 0.2, c - r * 0.1), fill=(255, 210, 200, 60))
    layer.alpha_composite(hl.filter(ImageFilter.GaussianBlur(6)))
    shadow = Image.new("RGBA", layer.size, (0, 0, 0, 0))
    ImageDraw.Draw(shadow).polygon([(x + 4, y + 6) for x, y in pts], fill=(0, 0, 0, 120))
    card.alpha_composite(shadow.filter(ImageFilter.GaussianBlur(4)), (int(at[0] - c), int(at[1] - c)))
    card.alpha_composite(layer, (int(at[0] - c), int(at[1] - c)))


def whole_card(card, f, w):
    if f == "Golden":
        g = ImageOps.grayscale(card.convert("RGB"))
        gold = ImageOps.colorize(g, (58, 36, 8), (255, 226, 150), mid=(196, 148, 64)).convert("RGBA")
        gold.putalpha(card.split()[-1])
        card = Image.blend(card, gold, 0.78)
    if f == "Shadow":
        rgb = np.asarray(card.convert("RGB")).astype(float)
        lum = rgb.mean(-1, keepdims=True) / 255
        night = np.concatenate([30 + 60 * lum, 20 + 90 * lum, 60 + 170 * lum], -1)
        out = Image.fromarray(np.clip(rgb * 0.35 + night * 0.65, 0, 255).astype(np.uint8)).convert("RGBA")
        out.putalpha(card.split()[-1])
        card = out
    if f == "Misprint":
        r, g, b, a = card.split()
        card = Image.merge("RGBA", (ImageChops.offset(r, 7, 3), g, ImageChops.offset(b, -6, -2), a))
    return card


def stamps(card, w, gs):
    f = finish(w, gs)
    shares, _, traits = mock4.info(w)
    tilt = -9 if f != "Misprint" else 172
    ink_stamp(card, "REVEALED", (CW - 200, 590), (196, 36, 44), tilt, w["id"], 38)
    if w["start"]:
        wax_seal(card, (CW - 96, 196), 48, w["id"])
    if any("Satellite" in t for t in traits) or any(l["world"] == w["id"] and "Gravitas" in l["prefab"] for l in D["landmarks"]):
        ink_stamp(card, "CLASSIFIED", (200, 220), (34, 30, 36), 7, w["id"] + 5, 32, paper=True)
    d = ImageDraw.Draw(card)
    s = serial(w)
    d.text((CW - 50 - d.textlength(s, font=mock.font(15)), 958), s, font=mock.font(15), fill=(200, 194, 176))
    return card


def render(w, index):
    gs = mock4.info(w)[1]
    card = mock4.card_full(w, index)
    card = whole_card(card, finish(w, gs), w)
    card = stamps(card, w, gs)
    clip = Image.new("L", (CW, CH), 0)   # nothing may spill past the card's own edge
    ImageDraw.Draw(clip).rounded_rectangle((3, 3, CW - 4, CH - 4), 20, fill=255)
    card.putalpha(ImageChops.multiply(card.split()[-1], clip))
    return card


mock4.DECORATE, mock4.TITLE, mock4.NUMBER = decorate, title, number
mock4.HOLO = lambda w, gs: finish(w, gs) in ("Holo", "Aurora", "Gilded", "Golden")


def sheet(cards, labels, out, title_text):
    cols = 4
    rows = (len(cards) + cols - 1) // cols
    W, H = 80 + cols * (CW + 30), 240 + rows * (CH + 80) + 20
    canvas = Image.new("RGBA", (W, H), (24, 22, 28, 255))
    canvas.alpha_composite(mock4.outlined(mock4.headline(title_text, 72), 3), (80, 60))
    d = ImageDraw.Draw(canvas)
    d.text((84, 150), f"{D['colony']}  ·  {D['seed']}", font=mock.font(26), fill=(170, 166, 156))
    for i, (c, lab) in enumerate(zip(cards, labels)):
        x, y = 80 + (i % cols) * (CW + 30), 240 + (i // cols) * (CH + 80)
        canvas.alpha_composite(c, (x, y))
        d.text((x + CW / 2 - d.textlength(lab, font=mock.font(28)) / 2, y + CH + 18), lab, font=mock.font(28), fill=(220, 214, 196))
    canvas.convert("RGB").save(out, quality=88)
    print(out, canvas.size)


if __name__ == "__main__":
    OUT = mock4.OUT
    show = next(w for w in WORLDS if w["name"] == "Contamista")
    gallery = ["Plain", "Etched", "Holo", "Gilded", "Frost", "Molten", "Tidal", "Aurora", "Golden", "Shadow", "Misprint"]
    cards = []
    for f in gallery:
        FORCE = f
        cards.append(render(show, 2))
    FORCE = None
    sheet(cards, gallery, os.path.join(OUT, "h-gallery.jpg"), "FINISHES")
    real = [render(w, i + 1) for i, w in enumerate(WORLDS)]
    sheet(real, [finish(w, mock4.info(w)[1]) for w in WORLDS], os.path.join(OUT, "h-set.jpg"), D["colony"].upper())
