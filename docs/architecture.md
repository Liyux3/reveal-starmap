# Architecture

## Preview

`RevealSessionCoordinator` owns mutually exclusive StarMap, World and Canvas previews.
`ClusterMapPreview_Patches` changes display queries in UI scopes; rocket pathfinding
continues to read real fog. `PropertyTextures_FogOfWar_Patch` changes the fog texture
without writing terrain visibility. Session-local camera snapshots restore world,
position and actual displayed zoom. World switching uses the stock Oversee Planetoid
contract, `SetActiveWorld` followed by `SnapTo`, so no delayed fade callback can outlive
a preview. Preview zoom limits clamp both current and target zoom. World/canvas previews end on reload; starmap preview may persist.

`RevealSnapshotState` retains the old serialized field names for compatibility.
It is attached during `SaveGame.OnPrefabInit`, before `SaveLoadRoot.LoadInternal` looks
up components for deserialization. The input controller starts at `OnSpawn`, after loading.
`RevealSnapshotStore` and `GridRevealSnapshotStore` restore legacy mutable previews once.
Legacy axial coordinates restore in native `(R, Q)` order; snapshot validation rejects
missing lists and overflowed dimensions. New previews do not capture or restore exploration progress, preserving research earned
while a preview is active. Camera bounds apply to the active world or full canvas.

## Permanent Survey

`RevealStarMapSettings.StarMapAction` defaults to TemporaryPreview. All preferences live in
PLib's shared mod config and apply across saves. Selecting PermanentSurvey explicitly opts
the starmap shortcut into immediate permanent analysis; consequences are disclosed in that
option's tooltip. Loading a save only reads the preference. The shortcut is the sole trigger.
There is no per-save consent or onboarding. The old per-save booleans were removed; native
KSerialization's unknown-field skipping is covered by a managed integration test.

`StarMapSurvey.Prepare` copies the real fog dictionary and raises only the current,
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
Input respects application focus, loading, native fades, modal screens, text fields and
exact modifiers. The stock starmap disables colony camera input while open, which does
not disable the mod shortcuts. Settings revisions trigger
actionable shortcut-conflict feedback; mode and preference events refresh the native world
button's label and tooltip. General feedback defaults to Off and can be enabled globally.
EveryAction retains its existing numeric enum value; retired first-use values become Off.
Legacy ShowNotification and SurveyConfirmation JSON fields are ignored on read and omitted
when settings are next saved. Existing shortcut and explicit EveryAction values are preserved.

`RevealText` loads keyed catalogs for English, Simplified Chinese and Traditional Chinese.
All three catalogs are embedded in the DLL as fallbacks for their matching locale. Mod language can follow the game or be
overridden; standard PLib window controls follow the game. New notification objects store
text keys and arguments so their bodies follow language changes; legacy bodies still load.
See [localization](localization.md) for catalog rules.

## Build and Delivery

`build-env.sh` resolves the installed .NET SDK and explicit or default ONI managed path.
The shell build and project file use the pinned `vendor/PLib.dll`; arbitrary subscribed
mod copies are never selected. `build.sh` stages a clean package, includes the PLib MIT
notice, moves the preceding generated folder to system Trash and emits a versioned ZIP.
`check.sh` builds once, lints shell scripts when ShellCheck is installed, and runs the
managed verifier against actual game assemblies. Assembly and package versions,
embedded catalogs, PLib identity and every archive file are verified together.
