# Reveal StarMap Memory

## Intent

User request, 2026-10-26, verbatim:

> 本地最新的是什么，你再都从头到尾都看好，正式做到最优雅漂亮的状态并提交。

Publication request, 2026-10-26, verbatim:

> 全都弄好，然后直接上线提交就好了。

Earlier user wording retained for this review:

> 不需要每个存档都要弄啊，就只需要你的像这种配置的话，其实只需要在模的那个对应的 config 菜单里面

> 所有状态都应该有黑边界镜头限制，缩放应该也有对应的。

Criteria: shared configuration; quiet reversible defaults; rare permanent behavior in
settings; consistent English and both Chinese variants; native controls; world/canvas
bounds; reliable restoration and migration; clean packaging and verified commits.

## Overview

Standalone `Oxygen Not Included: Spaced Out` debug mod focused on temporary map
visibility tools, with an optional global preference for permanent starmap survey.

Workshop's last verified published version is `0.3.1.0`. Qualified package: `0.4.1.0`,
with 46 managed checks and 26 final native assertions passed. Shift+F7/F8, planet buttons,
camera restoration, native reload and permanent routes passed in the original mod profile.
Three fueled rockets failed native launch-readiness checks; full flight remains untested.
GitHub publication is authorized. Steam's native uploader is blocked by a Documents
permission prompt and the Mac lock; a byte-identical Library upload candidate is ready.
Current implementation: [architecture](docs/architecture.md).
Release evidence and remaining checks: [progress](docs/progress.md).

Primary distribution points:

- GitHub: `https://github.com/Liyux3/reveal-starmap`
- Steam Workshop item: `3695988693`

Current user-facing feature set:

- full starmap reveal and restore
- asteroid-side-screen `Preview map` / `End preview` button
- full live simulation canvas reveal and restore
- configurable hotkeys in `Mod Options`
- default reversible preview; optional permanent analysis for real rocket navigation
- global preferences for language, shortcuts, action and feedback; silent preview by default
- complete English, Simplified Chinese and Traditional Chinese catalogs

## Architecture Snapshot

Key runtime layers:

- `src/Runtime/RevealSessionCoordinator.cs` owns render-only previews and camera restoration.
- `src/Runtime/RevealStarMapRuntime.cs` routes preview versus confirmed permanent survey.
- `src/Runtime/StarMapSurvey.cs` prepares monotonic analysis updates without live mutation.
- `src/Runtime/RevealSnapshotStore.cs` migrates legacy fog snapshots and commits real surveys.
- `src/Runtime/GridRevealRuntime.cs` focuses worlds; terrain preview is handled by the fog texture patch.
- `src/Runtime/GridRevealSnapshotStore.cs` restores legacy grid snapshots once.
- `src/Patches/ClusterGridWorldSideScreen_Patch.cs`
  injects and refreshes the native preview button in asteroid details
- `src/Runtime/RevealStarMapController.cs`
  routes configured hotkeys for starmap and canvas actions

Persistent save state:

- `src/State/RevealSnapshotState.cs`
  stores preview metadata and retains legacy snapshot fields for migration

Build and dependency policy:

- `vendor/PLib.dll` is the pinned compatibility copy, currently `PLib 4.21`
- `scripts/build.sh` and `src/OniModStarter.csproj` use the pinned vendor copy;
  `scripts/check.sh` is the final build, managed and package gate
- target framework is `net48` to match current ONI / Harmony requirements

Config UI policy:

- default PLib enum options were replaced with custom dynamic option handlers in
  `src/Options/`
- reason: PLib enum option write-back stored strings and crashed on save for
  `RevealModifier` / `RevealFunctionKey`

Runtime test policy:

- DLC-only validation must launch ONI through Steam, not by opening the app
  bundle directly, otherwise `EXPANSION1_ID` mods may be skipped
- only keep one active `Reveal StarMap` instance during real-user testing
  because duplicate `Local` and `Steam` entries with the same `staticID`
  confuse ONI mod state and options UI
- check the shared `../.codex/oni-live-session.json` record and other active ONI
  tests before using the game process; preserve their process and mod profile

## Progress Log

### 2026-03-31

Created the standalone `Reveal StarMap` project from the earlier Infinite
Galaxy debug reveal logic.

Added:

- configurable hotkey reveal
- local install script
- GitHub repository
- Workshop packaging assets

### 2026-04-01

Implemented starmap reveal restore using persisted fog snapshots.

Added:

- `Shift + F7` style reveal toggle behavior
- save-persisted starmap snapshot restore
- improved Workshop description and preview asset handling

### 2026-04-02

Expanded the mod beyond pure starmap reveal.

Added:

- asteroid details `Reveal World Map` button
- full live simulation canvas reveal toggle
- camera-state restore after grid reveal
- notification logging into `Player.log`

Critical debugging outcomes:

- ONI build `719533` accepted the mod only when started through Steam with DLC
  context
- duplicate `Local` plus `Steam` copies of `Reveal StarMap` caused options UI
  confusion and were removed from the active test path
- old bundled `PLib.dll` crashed `Mod Options` on current ONI because of
  `TMPro.FaceInfo` dependency drift
- vendored `PLib 4.21` fixed the `TextMeshPro` compatibility crash
- PLib's built-in enum options handler still crashed when saving, so custom
  dynamic enum option handlers replaced it

### 2026-04-03

Prepared Workshop screenshots and captions in:

- `/Users/liyux/Downloads/steam-compressed`

Current recommended upload assets there:

- `01-starmap-reveal.jpg`
- `02-starmap-routing.jpg`
- `03-gameplay-scene.jpg`
- `captions.txt`
