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
route, save and quit. Buttons must be selected from current ui output. `route` reports
whether it used a real rocket or a temporary selector fixture; neither implies live flight.

The driver sets runInBackground only in the test process. Quit normally, remove both test
mods from Local, restore the backed-up config and enabled mod set while retaining Steam's
own downloads, and verify original campaign hashes. The resume-pointer command requires the existing original save path captured before testing.
`batch` runs ordered commands in one frame to exercise rapid preview transitions.
Snapshots report fade state, input readiness, actual/target camera zoom and rocket state.
`flight` accepts a live rocket instance ID and target axial coordinates, requires a native
route within its remaining range, and checks native launch readiness before starting.
Native paths omit their starting cell; path Count is the travel distance. No fuel, crew,
readiness or debug bypass is supplied. `pause` stops an active test run for inspection.
