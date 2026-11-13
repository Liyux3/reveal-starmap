# Architecture

## Preview

`RevealSessionCoordinator` owns mutually exclusive StarMap, World and Canvas previews.
`ClusterMapPreview_Patches` changes display queries in UI scopes; rocket pathfinding
continues to read real fog. `PropertyTextures_FogOfWar_Patch` changes the fog texture
without writing terrain visibility. Session-local camera snapshots restore world,
position and actual displayed zoom. World switching uses the stock Oversee Planetoid
contract, `SetActiveWorld` followed by `SnapTo`, so no delayed fade callback can outlive
a preview. Preview zoom limits clamp both current and target zoom. World/canvas previews end on reload; starmap preview may persist.

The mod's own world switches go through `SwitchActiveWorld`. A prefix on
`ClusterManager.SetActiveWorld` sees every other change (world selector, notifications,
rocket interiors) before it is announced: it ends a world or canvas preview where the
player is going, restoring normal zoom limits and cluster effects without moving the view
back. `UserNavigation` records the leaving world's camera on every change and reopens the
world there later, so a preview session copies those records when it begins and the
matching postfix puts each leaving world's record back: the starting world gets the view
the player had when the preview began, other worlds their earlier record or none.

Camera bounds apply in every state as a postfix on `ConstrainToWorld`. Vanilla stops
constraining once the view is larger than the world (common with zoom-out mods), which
let the camera drift into black space; the postfix centers it instead. Running there,
before `Update` publishes the camera to shaders and `VisibleArea`, keeps culling on the
final position. The canvas preview skips the vanilla world constraint and uses the canvas.

`RevealSnapshotState` retains the old serialized field names for compatibility.
It is attached during `SaveGame.OnPrefabInit`, before `SaveLoadRoot.LoadInternal` looks
up components for deserialization. The input controller starts at `OnSpawn`, after loading.
`RevealSnapshotStore` and `GridRevealSnapshotStore` restore legacy mutable previews once.
Legacy axial coordinates restore in native `(R, Q)` order; snapshot validation rejects
missing lists and overflowed dimensions. New previews do not capture or restore exploration progress, preserving research earned
while a preview is active. Camera bounds apply to the active world or full canvas.

## Map Export

Shift + F9 prints collectible cards of what the player is looking at: the previewed asteroid
during World preview, every asteroid of the cluster as one set sheet during Canvas preview, and
otherwise the active world with its real fog (rocket interiors print as unnumbered cards; an
active world with nothing explored gets a message instead). The design and its tier rules are
in [directions](directions.md#map-export-as-collectible-cards-design-approved-2026-11-13).

`MapExport` holds the camera only for the capture. For each card it leaves and re-enters
`DebugHandler.SetTimelapseMode` with the card's world (the override remembers the world it
replaces, and non-active worlds render dimmed), snaps a frame wide enough for tile meshes,
and one frame later renders the world cameras with `CameraController.RenderForTimelapser`
into its own texture at whole pixels per cell (long side near 2400 px). Camera, overlay,
timelapse mode and the freeze camera are restored as soon as the terrain is in memory; preview
zoom limits pause only while the camera is framed.

The card is pure computation and runs on worker threads, all cards of a set in parallel.
`Cards/CardData` reads the facts: spawned `Geyser` configurations, pending `WorldGenSpawner`
spawns (type known, output unknown until revealed), traits, the world's description, biome
shares, seed, cycle, and a SHA-256 of seed and world name that fixes every variant for a
seed. `Cards/CardCanvas` is a small straight-alpha raster (scanline polygons, coverage-buffer
strokes, index-masked composites, grain, blur, 2x supersampling). `Cards/CardComposer` draws
the card, and `Cards/CardArt` brings in the game's own pixels: TextMeshPro type (GRAYSTROKE for
Latin titles, the localized font otherwise), prefab UI sprites and the world's starmap sprite,
each read back through a render texture. Those calls are queued to the main thread and served
once per frame, so a worker never touches Unity. The sheet layout and PNG encoding
(`ImageConversion.EncodeArrayToPNG`) also run off the main thread.

Measured on the neutral test save (8 asteroids, on the Pro): a single card holds the camera
1.1 s and composes in about 10 s while the game keeps running; the 8-card set takes 3.4 s of
capture and 11 s of parallel composition, 15 s in all (42.6 s before threading), with frames
at 51 ms mean during composition and 19 ms after. Files go to
`Util.RootFolder()/RevealStarMap/exports/` as `Colony - World - Cycle N.png`, never
overwriting, and the path is always shown since the export is an explicit action.

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

PLib 4.21 is vendored and merged into `RevealStarMap.dll` with its types internalized, the
packaging PLib's author uses. A separate `PLib.dll` binds by simple name on Mono, so on Mac
and Linux another mod's older copy, loaded first, could stand in for the tested one; that
is the likely cause of the June options crash report. Each PLib copy now draws only the
dialogs of the mods that registered with it. Custom enum entries return actual enums on save. Dropdown minimum
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
mod copies are never selected. `build.sh` compiles, merges PLib with ILRepack (pinned as a
local .NET tool in `.config/dotnet-tools.json`), stages a clean package with the PLib MIT
notice, moves the preceding generated folder to system Trash and emits a versioned ZIP.
`check.sh` builds once, lints shell scripts when ShellCheck is installed, and runs the
managed verifier against actual game assemblies. Assembly and package versions,
embedded catalogs, PLib identity and every archive file are verified together.
A released version is immutable: when tag `vVERSION` exists, only its commit can build
archives with that version, so development starts by raising `mod_info.yaml`.

Workshop delivery is a separate contract: current `KMod.Steam.MakeMod` requires
`File.Exists(GetItemInstallInfo(...))`, then constructs a ZIP file source. A successful
Steam depot upload/download alone is insufficient. Build emits a root-level
`-workshop.zip` alongside the enclosing-folder Local-install archive; managed checks
validate both member layouts and hashes. Use the official uploader or a legacy ZIP
publication path, with creator app 636750 distinct from consumer game 457140.

`scripts/publish-workshop.sh VERSION NOTE_FILE` is that legacy path. It downloads the
Workshop ZIP from GitHub release `vVERSION`, so both channels carry identical bytes, then
`workshop-publish.c` stages it in Steam Cloud and commits only the file and a change note
through the uploader's RemoteStorage calls, leaving title, description, tags and preview
alone. Public readback must show a ZIP filename and the exact size, and a final download
as the game must install a regular file equal to the release ZIP. Receipts are written
before and after each remote write under `artifacts/workshop/`.

The sole Workshop target is `3695988693`. After conversion to modern storage,
legacy uploads completed with EResultFail (2) and retained the depot manifest.
The owner selected preservation of this identity; a private recovery duplicate was
deleted. Until an actual subscribed legacy ZIP is verified for the original item,
GitHub remains the qualified installation path. Preserve the restored user-authored
Workshop description when updating content.
