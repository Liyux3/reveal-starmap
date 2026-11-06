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

## Map export (built 2026-11-06, 0.5.0)

The user asked for candidate 4 done elegantly with sharing in mind. It shipped as Shift + F9
on the game's own timelapse capture; [architecture](architecture.md#map-export) owns the
mechanism. Next steps that build on it: a control in a preview bar beside the shortcut, and
asteroid flip-through (2), which turns exporting every asteroid of a seed into a few presses.
