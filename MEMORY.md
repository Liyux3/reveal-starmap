# Reveal StarMap Memory

## Intent

User request, 2026-10-26, verbatim:

> 本地最新的是什么，你再都从头到尾都看好，正式做到最优雅漂亮的状态并提交。

Publication request, 2026-10-26, verbatim:

> 全都弄好，然后直接上线提交就好了。

Publication identity decision, 2026-10-26, verbatim:

> 对。保留旧的，新的我不要重复，去掉。你把他优雅的弄好。

Recovery and acceptance request, 2026-10-30, verbatim:

> 什么情况呀？把都修好呀，然后把都端到端跑通。

> 用它的官方那个不是有个官方的工具吗？就是这样的工具。

Save protection, 2026-10-30, verbatim:

> 反正你不要影响我原来的游玩就行。

User takeover request, 2026-11-05, verbatim:

> 你用我pro的登录态。你去完全接手下我们那边RevealStarMap相关的项目，深刻理解整个项目和所有我和codex/claude的交互轨迹。0. 深刻理清楚主线，持续做好。1. 分发和更新/开源/营销 2. brainstorm思路/玩法和游戏底层机制的交互 3. 底层架构和稳健/效率/性能的最优化。 优雅并漂亮的做到最优。fully autonomous。3-30day

Rollback question, 2026-10-31, verbatim:

> 你为啥要历史版本回退呀？我们不是要更新版本吗？把它都弄到最优雅状态就行了呀。

Public images, 2026-11-06, verbatim:

> 商店记得别放我的基地。

Map export look, 2026-11-06, verbatim (after the first render export):

> 你这导出的太丑了，和截图没区别，那没有任何意义

> 要很有游戏感

Native acceptance uses additional copies under the project's ignored test directory.
Never load or save over personal campaign paths. Restore temporary mod preferences and
the original Continue pointer after the test session.

Earlier user wording retained for this review:

> 不需要每个存档都要弄啊，就只需要你的像这种配置的话，其实只需要在模的那个对应的 config 菜单里面

> 所有状态都应该有黑边界镜头限制，缩放应该也有对应的。

Criteria: shared configuration; quiet reversible defaults; rare permanent behavior in
settings; consistent English and both Chinese variants; native controls; world/canvas
bounds; reliable restoration and migration; clean packaging and verified commits.

## Overview

Standalone `Oxygen Not Included: Spaced Out` mod: temporary previews of the starmap,
any asteroid and the whole canvas that change only what is drawn, plus an optional global
Permanent survey for rocket routes. Positioning and candidate features: [directions](docs/directions.md).

Status, 2026-11-05:

- Released `v0.4.2.0` on GitHub (lightweight tag at 1d4254c); assets match the natively
  accepted DLL `55e05a40...` (`artifacts/e2e-20261105/acceptance.json`).
- GitHub credentials work only in the Pro's GUI session (keychain): run git and gh there
  through Codex; over ssh, and on the Air, they fail.
- Workshop `3695988693`: converted to modern storage on 26 October and, as of 5 November,
  provably stuck there. The web revert to 30 July set its pointer to the legacy handle
  14374718941853451483, which Steam now fails to download as a manifest. Legacy RemoteStorage
  commits and ISteamUGC SetAllowLegacyUpload both return EResultFail (2). Next step is the
  user's decision: Steam Support, a new legacy listing, or GitHub only.
- Support: Klei emailed 11/06 (livesupport@kleientertainment.com, from liyuxian1358@gmail.com);
  Steam Support ticket HT-875C-93XN-FPD6 submitted 11/06 (replies to liyuxian1358531@icloud.com).
  The Workshop comment reply is posted. If nothing is restored in three to five business days,
  publish a new legacy listing and point the old description at it.
- 0.5.0 candidate (not released): Shift + F9 map export, natively accepted 11/06
  (`artifacts/e2e-20261106c`); GitHub release only after the user approves the look.
- Steam web: the user signed in to Steam in the Pro's Edge on 5 November; drive owner pages
  with `web_browse` CSS selectors (inspect refs are not OpenCLI click refs). Never type
  passwords or codes or read cookies.

Rules that stay: never publish a modern depot directory; accept a release only when a
subscriber install is a regular ZIP equal to the release asset; keep the Workshop
description user-authored with point edits only; never load or save personal campaigns,
use copies under `artifacts/`; never show the user's own colony (the campaign.sav copies,
their exports or screenshots) on the Workshop page, GitHub release, README or any promotion,
and make public images from a fresh, neutral save; run native tests through `tests/native-session.py`, which
shares the install with other ONI projects through `mods/.native-validation.lock`.
Current implementation: [architecture](docs/architecture.md). History: [progress](docs/progress.md).

Primary distribution points:

- GitHub: `https://github.com/Liyux3/reveal-starmap`
- Steam Workshop item: `3695988693`

Current user-facing feature set:

- full starmap reveal and restore
- asteroid-side-screen `Preview map` / `End preview` button
- full live simulation canvas reveal and restore
- world and canvas previews end when the player switches world, leaving saved views untouched
- configurable hotkeys in `Mod Options`
- Shift + F9 framed PNG export of the previewed asteroid, canvas or active world
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

- `vendor/PLib.dll` is the pinned compatibility copy, currently `PLib 4.21`; since 0.4.2
  `scripts/build.sh` merges it into `RevealStarMap.dll` (ILRepack, internalized), so the
  package has no separate `PLib.dll` for another mod's copy to shadow on Mono
- `scripts/check.sh` is the final build, managed and package gate; a released tag's version
  cannot be rebuilt from other code
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
