# Verification

`Verify.csproj` runs the managed checks documented in the main README.

`LiveProbe.cs`, `build-live-probe.sh` and `live-command.sh` are a local ONI test driver,
excluded from the managed verifier and production build. Their root is deliberately scoped
to the ignored `artifacts/e2e-20260922` evidence folder. They are never Workshop content.

The driver must be installed as a separate local test mod beside an enabled candidate with
a unique test static ID. Steam may replace a modified subscribed DLL; duplicate static IDs
also cause loader ambiguity. Back up mods.json, the subscribed package and mod config first.
Keep campaign inputs as copies under the evidence root. Load/save commands only accept
save basenames there. Use LoadingOverlay.Load for native reloads, then wait for IsLoading
to clear before recording a snapshot. The driver pauses newly loaded campaigns.

Commands exchange JSON via command.json/result.json, with a unique request ID and archived
responses. Examples: options, option, ui, click, load, snapshot, starmap, canvas, world,
route, save and quit. Buttons must be selected from current ui output. `route` reports
whether it used a real rocket or a temporary selector fixture; neither implies live flight.

The driver sets runInBackground only in the test process. Quit normally, remove both test
mods from Local, restore the backed-up config and enabled mod set while retaining Steam's
own downloads, and verify original campaign hashes. The resume-pointer command is scoped
to the original campaign for this recorded run; update it for a different test environment.
