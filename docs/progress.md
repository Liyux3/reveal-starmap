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
