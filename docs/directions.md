# Directions

## Position

Reveal StarMap lets a player look at everything and then put it back. Previews change
only what is drawn: exploration, research, telescope rewards, achievements and the save
stay as they were, and no hidden cell joins the simulation. The alternatives each give
something up. Debug mode reveals everything but disables achievements and makes every
revealed cell simulate, which slows large maps for the rest of the save. Reveal Map
(Workshop 3130257075, 73 ratings) avoids debug mode, but its reveal is permanent and
players report missing buildings and critters after revealing secondary asteroids.
Permanent survey stays a deliberate, documented choice for players who want to fly.

What players use it for, from comments, forum threads and the mod's own history:

- Scouting a new seed: look at every asteroid's terrain and geysers before committing.
- Planning routes: see where asteroids and POIs sit before telescopes reach them.
- Debugging and large clusters: check canvas use and world placement (Infinite Galaxy).
- Getting rockets to revealed cells, the most common confusion (comment of 8 Aug):
  answered by Permanent survey, which the Workshop description should name.

## Mechanics that shape every feature

- Spaced Out starmap: `ClusterFogOfWarManager` keeps reveal points per hex; 100 points
  analyze a hex, telescopes add points and pay 3 Data Banks per hex analyzed. Rocket
  paths read the real points, so a preview can show a hex without making it reachable.
- Terrain: `Grid.Visible` decides what is discovered and what simulates. The fog texture
  is a separate rendering input, which is why previews touch only the texture.
- Base game starmap: `SpacecraftManager.destinationAnalysisScores` keeps points per
  destination; 50 discovers and 100 completes it, and UI reads
  `GetDestinationAnalysisState`. The same split between display and real state applies.
- Achievements: the game disables them through debug and sandbox flags, which this mod
  never writes.

## Candidates

Each line gives value, cost and fit with the mod's quiet, native style.

1. Base game support. Shift+F8 previews the whole asteroid (the seed-scouting case for
   most players), Shift+F7 previews destination identities on the base starmap, and
   Permanent survey analyzes destinations. Value: the largest audience gain available.
   Cost: medium, one subsystem plus base-game native tests. Fit: high; same controls.
2. Asteroid flip-through. While a world preview is active, step to the next or previous
   asteroid without returning to the starmap. Value: makes seed scouting take seconds.
   Cost: low. Fit: high if it reuses the side-screen button row.
3. Canvas labels. During the canvas preview, name each world and show how much canvas is
   free for rocket interiors and new worlds. Value: the original purpose of Shift+F8.
   Cost: medium (screen-space labels). Fit: medium.
4. Map export (built in 0.5). Save a large PNG of a previewed asteroid or the canvas for sharing.
   Value: players share seeds as images, and every shared image advertises the mod.
   Cost: medium. Fit: medium.
5. Survey radius. Permanent survey only within a chosen distance of explored space.
   Value: a fairer middle ground. Cost: low. Fit: low, since it adds a setting few need.

Recommended order: ship the current fixes, then 1 and 2 together as 0.5, then 3 or 4
depending on what players ask for. 5 waits for a request.

## Map export as collectible cards (design approved 2026-11-13)

The first export (a framed render) was rejected as a big screenshot; the user asked for something
with game feel, then for stamp and card collecting, then for foil, filigree, gold, hidden variants
and stamps of every kind. The approved design is a full-art trading card per asteroid, made only
from the game's own material, and a set sheet for the cluster.

- Art: the asteroid's revealed terrain, rendered through the timelapse capture with the target
  world active (other worlds come out dimmed otherwise), covering the card; geyser UI sprites
  (`Def.GetUISprite`) pinned at their cells, unidentified `GeyserGeneric` spawns as small "?" marks.
- Chrome: wobbly ink outline with paper grain, title plate in GRAYSTROKE REGULAR SDF with an ink
  rim, the world's `ClusterGridEntity.GetUISprite()` in a round badge, a facts panel over the
  darkened lower art: traits or Start Asteroid, rarity marks, geyser tiles (one per kind, count
  badge, g/s once analyzed), the world's own description as flavor text, set code (seed cluster
  code such as SNDST-C), collector number, serial and seed.
- Geysers: spawned `Geyser` components with their configuration plus pending `WorldGenSpawner`
  spawns, which carry the type but no output until revealed.
- Tiers, all deterministic per seed so a shared card can be reproduced: Common; Uncommon (2+ kinds
  or 3+ geysers, silver etched frame); Rare (any metal volcano or 5+ kinds, holo sheen);
  Ultra (start asteroid or 3+ metal volcano kinds, gold guilloche, corner rosettes, gold title).
  Themed frames when one biome covers 60%: Frost, Molten, Tidal. Hidden variants from
  h = sha256(seed|world name)[:8]: h%100==2 Misprint, h%64==3 Golden, h%48==5 Shadow,
  h%16==0 Aurora; hidden cards are numbered past the set size (09/08).
- Stamps and marks: REVEALED red ink when exported from a preview (SURVEYED blue when the
  world is explored), wax seal with the printing pod on the start asteroid, a CLASSIFIED paper
  label for Crashed Satellites or Gravitas sites, a 1st edition mark at cycle 1, serial
  No. = h % 9999 + 1; planned: lucky-seed clover (3+ repeated digits), veteran stamp (cycle 1000+).
- Reference implementation: `design/cards/mock6.py` (with the earlier passes it imports) draws
  all of it from a LiveProbe run (`dump`, `assets`, World-preview renders); the C# port follows
  it. Public images use the neutral save only, never the user's colony.

## Map export (built 2026-11-06, 0.5.0)

The user asked for candidate 4 done elegantly with sharing in mind. It shipped as Shift + F9
on the game's own timelapse capture; [architecture](architecture.md#map-export) owns the
mechanism. Next steps that build on it: a control in a preview bar beside the shortcut, and
asteroid flip-through (2), which turns exporting every asteroid of a seed into a few presses.
