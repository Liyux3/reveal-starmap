# Architecture

## Preview

`RevealSessionCoordinator` owns mutually exclusive StarMap, World and Canvas previews.
`ClusterMapPreview_Patches` changes display queries in UI scopes; rocket pathfinding
continues to read real fog. `PropertyTextures_FogOfWar_Patch` changes the fog texture
without writing terrain visibility. Session-local camera snapshots restore world,
position and zoom. World/canvas previews end on reload; starmap preview may persist.

`RevealSnapshotState` retains the old serialized field names for compatibility.
`RevealSnapshotStore` and `GridRevealSnapshotStore` restore legacy mutable previews once.
New previews do not capture or restore exploration progress, preserving research earned
while a preview is active. Camera bounds apply to the active world or full canvas.

## Permanent Survey

`RevealStarMapSettings.StarMapAction` defaults to TemporaryPreview. Saving options only
updates configuration. `RevealStarMapRuntime` requests a native confirmation when the
hotkey is used in PermanentSurvey mode, guarded against duplicate dialogs and save changes.
The confirmed cell list is fixed when the dialog opens; analysis points are re-read at
confirmation to preserve research earned in the meantime.

`StarMapSurvey.Prepare` copies the real fog dictionary and raises only the confirmed,
valid cells to `ROCKETRY.CLUSTER_FOW.POINTS_TO_REVEAL`, preserving higher values and other
entries. Preparation is side-effect free; commit swaps the dictionary once. A single
`ClusterFogOfWarRevealed` event with null location invalidates routes and telescope targets
without per-asteroid discovery popups. It retains native fuel, path, range and landing checks.
The resulting points use native serialization, persist without this mod, and have no undo.

No debug/sandbox/instant-build or achievement flags are written. Terrain visibility and
planet visit records are untouched. Telescope work and its Data Bank rewards are skipped.
Third-party event subscribers can have additional effects; callback exceptions are logged
after the dictionary commit. No temporary navigation bypass is used.

## Options and Compatibility

PLib 4.21 is vendored. Custom enum entries return actual enums on save. Dropdown minimum
widths use layout units, avoiding the older `SetMinWidthInCharacters` font-metric dependency.
`IOptions.OnOptionsChanged` applies settings immediately; unknown enums select safe defaults.
Input respects modal screens, text fields and exact modifiers. `RevealText` localizes the
new behavior selector and confirmation using the game's Chinese/English locale.
