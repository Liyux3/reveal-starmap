# Progress and Validation

## 2026-07-30: Published 0.3.1.0

Published the render-only preview and camera-bound fixes through the official ONI uploader
to Workshop item `3695988693`. Public API timestamp: `1785435334`; archive: 166398 bytes.
The clean upload folder is outside Documents at
`~/Library/Application Support/RevealStarMapWorkshopUpload/content` to avoid duplicate
files observed in the synced dist folder. SteamCMD workshop uploads do not support this app.

## 2026-09-19: 0.4.0.0 Candidate

Investigated both Workshop comments. The navigation failure is explained by render-only
UI patches while native `ClusterGrid.GetPath` requires real FOW analysis. Added an explicit
Permanent Survey option alongside the default reversible preview. Native confirmation
documents permanence, research reward loss, navigation limits and achievement behavior.
Settings changes alone never execute a survey. World/canvas tools remain render-only.

The June settings-crash report has no stack trace, so its exact cause remains unconfirmed.
Prior fixes vendored PLib and replaced enum string write-back. This candidate additionally
avoids PLib font-metric calls when sizing dropdowns and applies confirmed options immediately.

Validation: built against locally installed ONI managed assemblies, latest available log
build U59-744825-SCRPAN. Re-decompiled current ClusterGrid, ClusterFogOfWarManager and
ColonyAchievementTracker; their pathfinding and achievement gates match the implementation.
Sixteen managed integration checks pass, including real PLib OptionsEntry.WriteTo and
the game's Newtonsoft.Json serializer. Native ONI save/reload is part of the pending live test.

Live UI validation was blocked by Computer Use ScreenCaptureKit error -3811. Still required:
open/save/reopen options; preview while researching and end it; cancel survey; confirm in a
disposable save, route a fueled rocket, then save/reload; check world/canvas restore and bounds.
The candidate has not been uploaded. Steam remains on 0.3.1.0.

Steam Remote Storage reports `banned=0` and `visibility=0` (public). The earlier removed/
incompatible claim came from hidden page templates and is withdrawn.

## 2026-09-19: Interaction and Language Polish

Grouped settings into Starmap, Canvas and Preferences. Added a default game-language
preference with English, Simplified Chinese and Traditional Chinese catalogs. Reused
enum choice construction and centralized all mod-provided user-facing text. Native
buttons keep game styling and measured spacing; mode/settings changes refresh their labels.

Permanent survey now defaults to one confirmation per save, recorded after successful
commit, with an Every time preference. Already surveyed maps no longer open a modal.
Routine feedback defaults to first use; Every action and Off are configurable. Old muted
configs remain muted, and new message bodies follow language changes. Error confirmations
use a single OK action. Long explanations moved into settings tooltips and docs.

Final module validation: 31 managed checks passed against the shipped DLL, actual PLib
write-back and all three catalogs. The native capture retry still returned -3811. Unity
dialog layout, in-game clicking, native save/reload and flight validation remain pending.
Candidate remains 0.4.0.0; Workshop remains 0.3.1.0.

## 2026-09-19: Global Preferences and Packaging

User requested one mod-level configuration across campaigns with minimal interruption.
Removed per-save consent and first-use notifications, along with their stored booleans.
All behavior preferences use the shared mod config. Defaults are reversible preview,
game language and silent operation. Permanent Survey is an explicit config choice; its
shortcut immediately analyzes the current map. Ordinary preview text no longer discusses
navigation analysis. Errors still receive actionable feedback.

Removed redundant confirmation settings, dialogs and translation keys. Retired draft
notification values migrate to Off while preserving shortcuts and explicit EveryAction.
Thirty-three managed checks passed, including the actual game's KSerialization mapping
skipping removed bool fields without losing retained preview state. Packaged version
remains 0.4.0.0 and is not yet uploaded; Unity visual/flight validation remains outstanding.

Native startup check: launched through Steam on U59-744825 with the candidate installed.
Player.log recorded RevealStarMap.dll loading and `[Reveal StarMap] Loaded.`; main menu
rendered successfully. System full-display capture worked after window capture failed.
The update notice closed with Escape, but automated mouse clicks did not open the Mods
page, so option-menu interaction is still unverified. No save was entered or modified.
Quit gracefully via the app's Quit event. Restored the subscribed 0.3.1.0 files and Reveal
enablement, verified all mods' enabled/DLC arrays against the pre-test backup, and verified
that the user's config remained unchanged. Steam's own updater updated two other subscribed
mods during startup; their new files and metadata were left intact.

## 2026-09-22: Live Integration and Reload Fix

Executed the candidate in ONI U59-744825-SCRPAN using a separate test-only local driver.
The shipped assembly was unchanged apart from the resulting fix below; the driver used
real PLib dialogs, their option entries and KButton callbacks, native game loading,
serialization and ClusterGrid.GetPath. This is in-engine integration coverage, not a claim
of reliable physical mouse automation or a complete launch/arrival flight.

Passed:

- Native options opened, saved and reopened with Simplified/Traditional Chinese and F7/F8
  bindings. Saved JSON matched the selections. No PLib enum or TextMeshPro exception.
- In an isolated eight-world save, preview kept analysis at 24/397 cells. FOW dictionary,
  terrain visibility and spawnable hashes were unchanged by star/world/canvas toggles.
- World/canvas exits restored the original camera state and world visit/discovery flags.
  An out-of-canvas camera position (-10000,-10000) was constrained to (5.86,3.40) at zoom 10.
- Permanent survey raised analysis to 397/397, with debug/sandbox flags still false and
  terrain hashes unchanged. The native route to the same formerly hidden cell became
  valid with 12 steps, and remained valid after native saving and cold reloading.
- In a separate copy containing 25 real clustercrafts, a real rocket's destination selector
  rejected a hidden cell before survey and returned a valid 16-step route afterward.
  Analysis rose from 79/547 to 547/547. No rocket was launched during this test.
- The actual world-side-screen button callback opened preview and changed its label from
  `預覽地圖` to `結束預覽`; its native layout was captured.

Found and fixed a real persistence bug: the preview metadata component was added only at
OnSpawn, so SaveLoadRoot skipped its serialized data. Attach it in OnPrefabInit instead.
Re-tested a save written during Canvas preview: after reload mode=None, zoom=20, free
camera=false, cluster FX restored; 397 analyzed cells persisted. All 33 managed checks also
passed with the fixed DLL.

An initial driver called DoLoad directly and hit BackwallManager/GroundRenderer during
scene teardown. It now uses the game's LoadingOverlay.Load wrapper; subsequent reload
and cold-load tests succeeded without that exception. Steam/Mod Updater also interrupted
early attempts. Testing used unique local mod identity and temporarily paused Mod Updater.

Cleanup: all originally enabled mods and the original config were restored and compared;
test mods moved out of Local, original Workshop DLL hash verified, original resume-save
pointer restored, and both source campaign files match their pre-test copies byte-for-byte.
Steam's own updates were retained; its removal of the already-disabled Worldgen Designer
entry was not reversed. No original campaign was saved by the test driver.

Evidence is under ignored `artifacts/e2e-20260922/`: snapshots, route results, native logs,
screenshots, isolated save files and the archived test installation. The candidate is still
0.4.0.0; Workshop remains 0.3.1.0. Remaining acceptance: physical input and full rocket flight.
