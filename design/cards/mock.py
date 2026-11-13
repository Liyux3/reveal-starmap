# Offline mockups of the information map, drawn from a LiveProbe world dump.
# usage: uv run --with pillow --with numpy mock.py DATA_DIR OUT_DIR
import json, sys, os
import numpy as np
from PIL import Image, ImageDraw, ImageFont

DATA, OUT = sys.argv[1], sys.argv[2]
os.makedirs(OUT, exist_ok=True)
D = json.load(open(os.path.join(DATA, "dump.json")))
FONT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "fonts", "NotoSansUI-Regular.ttf")
ELEM = D["elements"]
STATE = np.array([{"solid": 0, "liquid": 1, "gas": 2, "vacuum": 3}[e["state"]] for e in ELEM])
ZN = D["zoneNames"]
# The game's zone colours repeat (Barren = Sandstone, Rust = Metallic, Moo = Forest) and put Ocean and
# Jungle on the same pink, so biomes get a curated palette that keeps each one distinct.
BIOME = {"Sandstone": "#D9B45A", "Barren": "#9E9180", "BoggyMarsh": "#7E9A4E", "Forest": "#5E9F57",
         "ToxicJungle": "#B27BA9", "Ocean": "#4D8FB5", "FrozenWastes": "#A9D2DF", "IceCaves": "#C7DFEE",
         "MagmaCore": "#C8462E", "OilField": "#6A4A38", "Wasteland": "#C08A5C", "Rust": "#C9853A",
         "Swamp": "#79A68E", "Radioactive": "#9DD44C", "Metallic": "#8E99A6", "Moo": "#A9BE6A",
         "CrystalCaverns": "#9B7BC6", "CarrotQuarry": "#D79A6B", "SugarWoods": "#9CC9A0", "Space": "#8F8C88"}
ZONE = [tuple(int(c[i:i + 2], 16) for i in (1, 3, 5)) for c in
        (BIOME.get(n, z) for n, z in zip(ZN, D["zoneColours"]))]
import re
for e in D["elements"]: e["name"] = re.sub(r"<[^>]+>", "", e["name"])
SPACE = ZN.index("Space")

# Liquids carry the information players look for, so they get a curated, legible palette.
LIQUID = {"Water": "#3C8DDB", "DirtyWater": "#8C8A3E", "SaltWater": "#6FB7C9", "Brine": "#A7D3D6",
          "Magma": "#E2572B", "CrudeOil": "#4A3328", "Petroleum": "#8A5A2B", "Ethanol": "#B07CC6",
          "LiquidCarbonDioxide": "#7A7F87", "LiquidSulfur": "#D8C24A", "SugarWater": "#D99BB7",
          "LiquidPhosphorus": "#C7B9A0", "Chlorine": "#7BC47F", "MoltenGlass": "#E89A4A"}
GEYSER = {  # prefab suffix: (short name, output family colour)
    "steam": ("Cool Steam Vent", "#9CC3D5"), "hot_steam": ("Steam Vent", "#B9D6E3"),
    "hot_water": ("Water Geyser", "#3C8DDB"), "slush_water": ("Cool Slush Geyser", "#7FB4E6"),
    "filthy_water": ("Polluted Water Vent", "#8C8A3E"), "slush_salt_water": ("Cool Salt Slush", "#6FB7C9"),
    "salt_water": ("Salt Water Geyser", "#6FB7C9"), "small_volcano": ("Minor Volcano", "#E2572B"),
    "big_volcano": ("Volcano", "#E2572B"), "liquid_co2": ("CO₂ Geyser", "#7A7F87"),
    "hot_co2": ("CO₂ Vent", "#7A7F87"), "hot_hydrogen": ("Hydrogen Vent", "#E07AA8"),
    "hot_po2": ("Hot Polluted O₂ Vent", "#9C9A55"), "slimy_po2": ("Infectious PO₂ Vent", "#9C9A55"),
    "chlorine_gas": ("Chlorine Vent", "#7BC47F"), "methane": ("Natural Gas Geyser", "#E8A14A"),
    "molten_copper": ("Copper Volcano", "#D98A4E"), "molten_iron": ("Iron Volcano", "#C7704F"),
    "molten_gold": ("Gold Volcano", "#E3B341"), "molten_aluminum": ("Aluminum Volcano", "#B8C2CC"),
    "molten_cobalt": ("Cobalt Volcano", "#5B7FD1"), "molten_tungsten": ("Tungsten Volcano", "#A9A9B8"),
    "molten_niobium": ("Niobium Volcano", "#8C7AB8"), "oil_drip": ("Leaky Oil Fissure", "#6B4A3A"),
    "liquid_sulfur": ("Sulfur Geyser", "#D8C24A")}


def hexrgb(h): return tuple(int(h[i:i + 2], 16) for i in (1, 3, 5))
def mix(a, b, t): return tuple(round(a[i] * (1 - t) + b[i] * t) for i in range(3))
def font(px): return ImageFont.truetype(FONT, px)


def geysers(world):
    found = {(g["x"], g["y"]): g for g in D["geysers"] if g["world"] == world["id"]}
    for p in D.get("pending", []):
        if p["world"] == world["id"] and p["prefab"].startswith("Geyser") and (p["x"], p["y"]) not in found:
            found[(p["x"], p["y"])] = {"x": p["x"], "y": p["y"], "prefab": p["prefab"]}
    out = []
    for g in found.values():
        key = g["prefab"].replace("GeyserGeneric_", "").replace("Geyser_", "")
        name, colour = GEYSER.get(key, ("Unidentified Geyser", "#8A8F99") if key == "GeyserGeneric" else (key.replace("_", " ").title(), "#BBBBBB"))
        out.append(dict(g, label=name, colour=colour))
    return sorted(out, key=lambda g: (-g["y"], g["x"]))


def landmarks(world):
    return [l for l in D["landmarks"] + D.get("pending", []) if l["world"] == world["id"] and l["prefab"] == "Headquarters"]


def cells(world):
    raw = np.fromfile(os.path.join(DATA, f"dump-{world['id']}.bin"), dtype=np.uint8).reshape(world["h"], world["w"], 4)
    return (raw[..., 0].astype(int) | (raw[..., 1].astype(int) << 8)), raw[..., 2].astype(int)


def terrain(world, theme):
    """Per-cell colours: biome tint on solids, liquids in their own colour, gases a faint tint."""
    elem, zone = cells(world)
    state = STATE[elem]
    ground = hexrgb(theme["ground"])
    solid_t = np.array([mix(z, ground, theme["solid"]) for z in ZONE])
    gas_t = np.array([mix(z, ground, theme["gas"]) for z in ZONE])
    img = np.where((state == 0)[..., None], solid_t[zone], gas_t[zone])
    ids = [e["id"] for e in ELEM]
    for i, e in enumerate(ids):
        m = elem == i
        if not m.any(): continue
        if STATE[i] == 1:
            img[m] = mix(hexrgb(LIQUID.get(e, ELEM[i]["colour"])), ground, theme["liquid"])
        elif e == "Katairite":
            img[m] = mix(hexrgb(theme["border"]), ground, theme["hard"])
        elif e == "Unobtanium":
            img[m] = ground  # the world's outer frame says nothing
    open_space = (zone == SPACE) & (state != 0)
    img[open_space] = ground
    # A one-cell darker rim where solid meets open space gives the terrain its shape.
    solid = state == 0
    rim = solid & ~(np.roll(solid, 1, 0) & np.roll(solid, -1, 0) & np.roll(solid, 1, 1) & np.roll(solid, -1, 1))
    img[rim] = (img[rim] * theme["rim"]).astype(int)
    return np.flipud(img.astype(np.uint8)), zone, state


def biome_share(zone, state):
    names, counts = np.unique(zone[zone != SPACE], return_counts=True)
    total = counts.sum()
    return sorted(((ZN[n], ZONE[n], c / total) for n, c in zip(names, counts)), key=lambda t: -t[2])


PRETTY = {"FrozenWastes": "Tundra", "BoggyMarsh": "Marsh", "ToxicJungle": "Jungle", "MagmaCore": "Magma",
          "OilField": "Oil", "Swamp": "Swamp", "Wasteland": "Wasteland", "Forest": "Forest", "Sandstone": "Sandstone",
          "Barren": "Barren", "Rust": "Rust", "Ocean": "Ocean", "Radioactive": "Radioactive", "Metallic": "Metallic",
          "Moo": "Moo", "IceCaves": "Ice Caves", "CrystalCaverns": "Crystal Caverns"}


def draw_world(world, theme, px, out):
    img, zone, state = terrain(world, theme)
    W, H = world["w"] * px, world["h"] * px
    m = theme["margin"]
    side = 360
    head = 150
    canvas = Image.new("RGB", (m + W + m + side + m, head + H + m), theme["ground"])
    canvas.paste(Image.fromarray(img).resize((W, H), Image.NEAREST), (m, head))
    d = ImageDraw.Draw(canvas)
    ink, mute, faint = theme["ink"], theme["mute"], theme["faint"]
    d.text((m, 40), world["name"], font=font(54), fill=ink, stroke_width=1, stroke_fill=ink)
    sub = f"{world['w']} × {world['h']} cells  ·  Cycle {D['cycle']}  ·  {D['seed']}"
    d.text((m, 108), sub, font=font(20), fill=mute)
    gs = geysers(world)
    ox, oy = m, head
    def at(x, y): return ox + (x + 0.5) * px, oy + (world["h"] - y - 0.5) * px
    for l in landmarks(world):
        cx, cy = at(l["x"], l["y"] + 1)
        r = 13
        d.polygon([(cx, cy - r), (cx + r, cy), (cx, cy + r), (cx - r, cy)], fill=theme["pod"], outline=theme["ground"], width=3)
    for i, g in enumerate(gs, 1):
        cx, cy = at(g["x"] + 1, g["y"] + 1)
        r = 15
        d.ellipse((cx - r, cy - r, cx + r, cy + r), fill=g["colour"], outline=theme["ground"], width=3)
        f = font(17)
        t = str(i)
        tw = d.textlength(t, font=f)
        d.text((cx - tw / 2, cy - 12), t, font=f, fill="#111317")
    # Side panel: geysers first (what players compare seeds by), then biomes.
    sx, y = m + W + m, head
    d.text((sx, y), "GEYSERS", font=font(16), fill=mute); y += 30
    for i, g in enumerate(gs, 1):
        d.ellipse((sx, y + 2, sx + 22, y + 24), fill=g["colour"])
        f = font(14); t = str(i); d.text((sx + 11 - d.textlength(t, font=f) / 2, y + 3), t, font=f, fill="#111317")
        d.text((sx + 34, y), g["label"], font=font(19), fill=ink)
        if "average" in g:
            d.text((sx + 34, y + 25), f"{g['average'] * 1000:,.0f} g/s average  ·  {g['temperature'] - 273.15:,.0f} °C", font=font(15), fill=mute)
            y += 56
        else:
            d.text((sx + 34, y + 25), "not yet uncovered", font=font(15), fill=faint)
            y += 56
    y += 24
    d.text((sx, y), "BIOMES", font=font(16), fill=mute); y += 30
    for name, col, share in biome_share(zone, state):
        d.rectangle((sx, y + 4, sx + 22, y + 22), fill=mix(col, hexrgb(theme["ground"]), theme["solid"]))
        d.text((sx + 34, y), PRETTY.get(name, name), font=font(18), fill=ink)
        t = f"{share * 100:.0f}%"; d.text((sx + side - 20 - d.textlength(t, font=font(16)), y + 2), t, font=font(16), fill=mute)
        y += 32
    y += 24
    d.text((sx, y), "LIQUIDS", font=font(16), fill=mute); y += 30
    elem, _ = cells(world)
    ids, counts = np.unique(elem[STATE[elem] == 1], return_counts=True)
    for i, c in sorted(zip(ids, counts), key=lambda t: -t[1])[:6]:
        e = ELEM[i]
        d.rectangle((sx, y + 4, sx + 22, y + 22), fill=mix(hexrgb(LIQUID.get(e["id"], e["colour"])), hexrgb(theme["ground"]), theme["liquid"]))
        d.text((sx + 34, y), e["name"], font=font(18), fill=ink)
        y += 32
    d.text((canvas.width - m - d.textlength("Reveal StarMap", font=font(15)), canvas.height - m + 18), "Reveal StarMap", font=font(15), fill=faint)
    canvas.save(out)
    print(out, canvas.size)


DARK = dict(border="#4B4458", ground="#15171D", ink="#ECEDF1", mute="#9AA0AD", faint="#5E6472", pod="#F2F2F2",
            solid=0.30, gas=0.84, liquid=0.10, hard=0.20, rim=0.80, margin=64)
PAPER = dict(border="#8A8494", ground="#F3EFE6", ink="#22242A", mute="#6B6F78", faint="#A3A7AE", pod="#22242A",
             solid=0.30, gas=0.86, liquid=0.05, hard=0.35, rim=0.86, margin=64)

def draw_cluster(theme, px, out):
    worlds = D["worlds"]
    X0 = min(w["x"] for w in worlds); Y0 = min(w["y"] for w in worlds)
    X1 = max(w["x"] + w["w"] for w in worlds); Y1 = max(w["y"] + w["h"] for w in worlds)
    m, head, gap = theme["margin"], 150, 0
    canvas = Image.new("RGB", (2 * m + (X1 - X0) * px, head + (Y1 - Y0) * px + m + 40), theme["ground"])
    d = ImageDraw.Draw(canvas)
    d.text((m, 40), D["colony"], font=font(54), fill=theme["ink"], stroke_width=1, stroke_fill=theme["ink"])
    nge = sum(len(geysers(w)) for w in worlds)
    d.text((m, 108), f"{len(worlds)} asteroids  \u00b7  {nge} geysers  \u00b7  Cycle {D['cycle']}  \u00b7  {D['seed']}", font=font(20), fill=theme["mute"])
    for w in worlds:
        img, zone, state = terrain(w, theme)
        ox, oy = m + (w["x"] - X0) * px, head + (Y1 - w["y"] - w["h"]) * px
        canvas.paste(Image.fromarray(img).resize((w["w"] * px, w["h"] * px), Image.NEAREST), (ox, oy))
        for g in geysers(w):
            cx, cy = ox + (g["x"] - w["x"] + 1) * px, oy + (w["h"] - (g["y"] - w["y"]) - 1) * px
            d.ellipse((cx - 7, cy - 7, cx + 7, cy + 7), fill=g["colour"], outline=theme["ground"], width=2)
        label = w["name"]
        f = font(22)
        tw = d.textlength(label, font=f)
        d.rounded_rectangle((ox + 8, oy + 8, ox + 8 + tw + 20, oy + 44), 6, fill=theme["ground"])
        d.text((ox + 18, oy + 12), label, font=f, fill=theme["ink"])
    canvas.save(out)
    print(out, canvas.size)


if __name__ == "__main__":
    start = next(w for w in D["worlds"] if w["start"])
    draw_cluster(DARK, 3, os.path.join(OUT, "c-cluster.png"))
    draw_world(start, DARK, 4, os.path.join(OUT, "a-dark.png"))
    draw_world(start, PAPER, 4, os.path.join(OUT, "b-paper.png"))
