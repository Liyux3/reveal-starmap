# Verification

`./scripts/check.sh` builds, checks shell scripts and runs `Verify.csproj`, including
legacy coordinate migration, language fallback, typed options and archive identity.
It also verifies the root-level legacy Workshop ZIP separately from the Local-install
archive. Check Steam's actual installed artifact is a regular ZIP file, then compare
every member against the qualified package; a modern directory download does not meet
ONI's installation contract.

`LiveProbe.cs`, `build-live-probe.sh` and `live-command.sh` are a local ONI test driver,
excluded from the managed verifier and production build. Their default evidence root is the ignored
`artifacts/live-tests` folder. Set `LIVE_TEST_ROOT` for a separate run, and copy the emitted
`probe-root.txt` beside the installed driver DLL. They are never Workshop content.

The driver must be installed as a separate local test mod beside an enabled candidate with
a unique test static ID. Steam may replace a modified subscribed DLL; duplicate static IDs
also cause loader ambiguity. Back up mods.json, the subscribed package and mod config first.
Check `../.codex/oni-live-session.json`, other project sessions and the running game before
staging a profile. Record the owner and start time; a shared game restart invalidates the
remaining run. Restore shared preferences only after the current owner releases the game.
Keep campaign inputs as copies under the evidence root. Load/save commands only accept
save basenames there. Use LoadingOverlay.Load for native reloads, then wait for IsLoading
to clear before recording a snapshot. The driver pauses newly loaded campaigns.

Commands exchange JSON via command.json/result.json, with a unique request ID and archived
responses. Examples: options, option, ui, click, load, snapshot, starmap, canvas, world,
research, readiness, route, flight, save and quit. Buttons must be selected from current ui output. `route` reports
whether it used a real rocket or a temporary selector fixture; neither implies live flight.

The driver sets runInBackground only in the test process. Quit normally, remove both test
mods from Local, restore the backed-up config and enabled mod set while retaining Steam's
own downloads, and verify original campaign hashes. The resume-pointer command requires the existing original save path captured before testing.
The test driver redirects native save writes, including autosaves, into its evidence
root and blocks Steam achievement/stat writes for that process. Keep these guards
installed before advancing a copied campaign, then remove the driver after acceptance.
`live-command.sh` waits up to 120 seconds for a response during native loading;
`LIVE_TEST_TIMEOUT_SECONDS` can set a different positive timeout. Reconcile a timed-out
request against `result.json` before sending another command.
`batch` runs ordered commands in one frame to exercise rapid preview transitions.
Snapshots report fade state, input readiness, actual/target camera zoom and rocket state.
`flight` accepts a live rocket instance ID and target axial coordinates, requires a native
route within its remaining range, and checks native launch preparation before requesting
normal boarding and launch. Native paths omit their starting cell; path Count is the
travel distance. `research` and `flight` require a fully loaded active save whose existing
path is inside `Driver.Root`; sibling prefixes, path traversal outside the root and symbolic
links below the root are rejected. `flight` also rejects native instant-build mode.
No fuel, crew, readiness, movement or debug bypass is supplied. `pause` stops an active
test run for inspection.

## Recovery driver build

From the repository root, compile once into the recovery evidence directory:

```sh
LIVE_TEST_ROOT="$PWD/artifacts/recovery-20261030" ./tests/build-live-probe.sh
```

The absolute root is written into `probe-root.txt`, so the game's working directory has
no effect. Driver installation and game execution are separate steps. For commands against
an already authorized test session, set `LIVE_TEST_ROOT` to that same absolute directory.

## Native research during starmap preview

`research` takes integer `r`/`q` and finite positive numeric `points`, for example
`{"action":"research","r":1,"q":2,"points":0.25}` with coordinates selected from the
current cluster. It validates the target using `ClusterGrid.IsValidCell` and calls
`ClusterFogOfWarManager.Instance.EarnRevealPointsForLocation` directly.
The result includes `requestedPoints`, `beforePoints`, `afterPoints`, entry-presence flags
and `revealCompleted`. Points are the raw native `m_revealPointsByCell` floats, with zero
plus a false presence flag for an absent entry. `revealCompleted` is the native return
value: partial progress and an already revealed cell can both return false.
An already revealed target therefore supplies no progress-retention evidence.

In a paused isolated save, capture a baseline, activate temporary starmap preview, and
capture `mode=StarMap`. Select a valid, incompletely researched cell, earn a positive
increment and archive the result. End preview and capture another snapshot. The target's
structured `revealPoints` entry must retain the returned `afterPoints`; compare the full
point dictionaries at the post-research and post-exit checkpoints. A completion may
also reveal neighboring cells through native `RevealLocation`/`PeekLocation` behavior.
Keep simulation paused throughout this case to isolate those native side effects.

## World-preview reload

Capture the original `activeWorld`, camera and grid hashes while paused. Use
`{"action":"world","world":WORLD_ID}` for a current snapshot world, wait until
`camera.fading=false` and capture `mode=World`. Save to a new basename under the evidence
root while preview is active, then reload that basename through `load`. Wait for
`loading` to clear and the camera fade to finish. Require `mode=None`, a bounded camera
in the loaded world, and unchanged `visible`, `spawnable`, native research points,
debug and sandbox flags. Exact pre-preview camera restoration is tested when ending a
preview in the same session; reloading uses the saved view and normalizes its bounds. Archive the
pre-preview, active-preview and post-reload responses. Resolve asynchronous transitions
between commands; `batch` operates within one frame.

## Actual rocket readiness and flight

`{"action":"readiness"}` inspects every real `Components.Clustercrafts` entry;
`{"action":"readiness","rocket":INSTANCE_ID}` inspects one current instance.
It evaluates `CraftModuleInterface.PopulateConditionSet` for `RocketPrep`, `RocketStorage`,
`RocketFlight` and `RocketBoard`, including the launchpad's conditions, and returns each
native condition's type, status, message, tooltip and UI visibility. It also reports native
`CheckPreppedForLaunch`, `CheckReadyToLaunch`, launch-request state and instant-build mode.
These diagnostics concern the current destination and leave it unchanged. A failed
`flight` preparation check includes diagnostics for the requested destination before
restoring the previous destination. A retrieval exception yields `available=false`, a
specific `gap` and any conditions already collected; missing booleans remain null.

Use the returned native messages/tooltips to satisfy fuel, engine, route, pad clearance,
loading and crew requirements through ordinary gameplay. Native `Failure` blocks launch;
`Warning` is allowed by the manual launch checks. `CheckPreppedForLaunch` covers preparation,
storage and flight, while `CheckReadyToLaunch` additionally covers boarding. The driver's
`flight` follows `Clustercraft.RequestLaunch`, allowing native boarding chores to run;
the game retains its full readiness check when it actually launches. A pending request
is a checkpoint, and successful launch/arrival requires subsequent simulation evidence.

Record a grounded rocket's ID, origin and intended target, then issue
`{"action":"flight","rocket":INSTANCE_ID,"r":TARGET_R,"q":TARGET_Q}`.
Prefer a rocket with repeat trips disabled through its normal UI so the destination stays
stable on arrival. Snapshot `cycle` is the native zero-based `GameClock.GetCycle()` and
`simTimeSeconds` is `GameClock.GetTime()`. Rocket snapshots include `launchRequested`,
`atDestination`, `destinationWorld`, `padWorld`, `repeat`, `pathAvailable`, `remainingNodes`,
`moveProgress` and `etaSeconds` from the destination selector, module interface and
`ClusterTraveler` API. ETA is a native estimate and may be non-finite at zero speed;
missing paths or components leave nullable fields empty.

Archive a launch transition, real location/progress changes over advancing simulation
time, and arrival at the requested coordinates with `atDestination=true` and travel
stopped. For an asteroid landing, also require `status=Grounded` and `padWorld` matching
the target world after the landing animation. Arrival at an empty hex can remain
`InFlight`. Require that final status: `Launching` can already have destination
coordinates because native launch advances the first path node before the animation ends.
A stationary craft, zero ETA or an accepted route/request alone supplies
insufficient flight evidence. End with `pause`, then save/reload and re-identify the rocket
from the new snapshot because Unity instance IDs change across loads.
