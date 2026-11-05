#!/usr/bin/env python3
"""Hold the shared ONI install for one native test session, then restore it exactly.

Usage: LIVE_TEST_ROOT=DIR tests/native-session.py SAVE [MINUTES]

Takes the exclusive lease other local mod projects use, snapshots the personal profile,
installs dist/liyux.RevealStarMap as a candidate beside the LiveProbe driver built into
DIR (tests/build-live-probe.sh), copies SAVE into DIR and launches ONI windowed. Drive
the game with tests/live-command.sh, ending with {"action":"quit"}. After ONI exits, or
after MINUTES (default 30) or Ctrl+C with a graceful quit, the profile is restored and the
lease released. The lease is never released while the game runs. If this process is
killed outright, a pending-restore record makes the next run restore the profile first.
"""
import fcntl, json, os, re, shutil, signal, subprocess, sys, time
from datetime import datetime, timezone
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
RUN = Path(os.environ["LIVE_TEST_ROOT"]).resolve()
APP = Path(os.environ.get("ONI_APP_PATH", Path.home() /
    "Library/Application Support/Steam/steamapps/common/OxygenNotIncluded/OxygenNotIncluded.app"))
PLAYER = APP / "Contents/MacOS/Oxygen Not Included"
USER = Path.home() / "Library/Application Support/unity.Klei.Oxygen Not Included"
MODS, LOCAL = USER / "mods", USER / "mods/Local"
PENDING = MODS / ".reveal-starmap-restore.json"
CANDIDATE, DRIVER = "liyux.RevealStarMapCandidate", "liyux.RevealStarMap.LiveProbe"
PROFILE = {"mods.json": MODS / "mods.json", "kplayerprefs.yaml": USER / "kplayerprefs.yaml",
           "unlocks.json": USER / "unlocks.json", "config": MODS / "config/RevealStarMap"}
UNITY_DOMAIN = "unity.Klei.Oxygen Not Included"
GAME = "^/.*OxygenNotIncluded.app/Contents/MacOS/Oxygen Not Included( |$)"
stop = False


def log(message):
    print(f"[{datetime.now():%H:%M:%S}] {message}", flush=True)


def game_running():
    return subprocess.run(["pgrep", "-f", GAME], capture_output=True).returncode == 0


def record(saved, created):
    PENDING.write_text(json.dumps({"profile": str(saved), "created": created}))


def snapshot(saved):
    saved.mkdir()
    presence = {}
    for name, path in PROFILE.items():
        presence[name] = path.exists()
        if path.is_dir():
            shutil.copytree(path, saved / name)
        elif path.exists():
            shutil.copy2(path, saved / name)
    # Launch arguments can persist screen settings in Unity's preferences domain.
    subprocess.run(["defaults", "export", UNITY_DOMAIN, str(saved / "unity.plist")], check=True)
    (saved / "presence.json").write_text(json.dumps(presence))
    record(saved, [])


def restore():
    pending = json.loads(PENDING.read_text())
    saved = Path(pending["profile"])
    presence = json.loads((saved / "presence.json").read_text())
    for name, path in PROFILE.items():
        if path.is_dir():
            shutil.rmtree(path)
        elif path.exists():
            path.unlink()
        if presence[name]:
            (shutil.copytree if (saved / name).is_dir() else shutil.copy2)(saved / name, path)
    subprocess.run(["defaults", "import", UNITY_DOMAIN, str(saved / "unity.plist")], check=True)
    for module in pending["created"]:
        shutil.rmtree(LOCAL / module, ignore_errors=True)
    PENDING.unlink()
    log("Profile restored from " + str(saved))


def install(saved):
    created = []
    for module in (CANDIDATE, DRIVER):
        (LOCAL / module).mkdir(parents=True)  # Fails if a directory this run did not create exists.
        created.append(module)
        record(saved, created)
    candidate, driver = LOCAL / CANDIDATE, LOCAL / DRIVER
    shutil.copytree(ROOT / "dist/liyux.RevealStarMap", candidate, dirs_exist_ok=True)
    manifest = candidate / "mod.yaml"
    manifest.write_text(manifest.read_text().replace("staticID: liyux.RevealStarMap", "staticID: " + CANDIDATE))
    for name in ("LiveProbe.dll", "probe-root.txt"):
        shutil.copy2(RUN / name, driver / name)
    (driver / "mod.yaml").write_text(f'title: "Reveal StarMap Live Probe"\ndescription: "Test driver"\nstaticID: {DRIVER}\n')
    shutil.copy2(ROOT / "mod_info.yaml", driver / "mod_info.yaml")

    profile = json.loads(PROFILE["mods.json"].read_text())
    for module in (CANDIDATE, DRIVER):
        profile["mods"].append({"label": {"distribution_platform": 0, "id": module, "title": module, "version": 0},
            "status": 1, "enabled": True, "enabledForDlc": [], "crash_count": 0,
            "reinstall_path": None, "staticID": module})
    for item in profile["mods"]:
        on = item["label"]["distribution_platform"] == 0 and item["label"]["id"] in (CANDIDATE, DRIVER)
        item.update(enabled=on, enabledForDlc=["EXPANSION1_ID"] if on else [], crash_count=0, reinstall_path=None)
    profile["mod_load_in_progress"] = False
    PROFILE["mods.json"].write_text(json.dumps(profile, indent=2))
    prefs = PROFILE["kplayerprefs.yaml"]
    text = re.sub(r"^(\s*Volume_\w+:)\s*.*$", r"\1 0", prefs.read_text(), flags=re.M)
    prefs.write_text(re.sub(r"^(\s*EXPANSION1_ID\.ENABLED:).*$", r"\1 1", text, flags=re.M))


def quit_and_wait():
    # Keep asking the driver to quit; never return while the game still runs.
    while game_running():
        try:
            (RUN / "command.json").write_text(json.dumps({"id": f"session-quit-{time.time():.0f}", "action": "quit"}))
            log("Asked the driver to quit; waiting for ONI to exit")
        except OSError as error:
            log(f"Could not ask the driver to quit ({error}); still waiting for ONI to exit")
        for _ in range(60):
            if not game_running():
                return
            time.sleep(2)


def main():
    def request_stop(*_):
        global stop
        stop = True
    signal.signal(signal.SIGINT, request_stop)
    signal.signal(signal.SIGTERM, request_stop)

    save, minutes = Path(sys.argv[1]).resolve(), float(sys.argv[2]) if len(sys.argv) > 2 else 30
    lock = (MODS / ".native-validation.lock").open("a+")
    try:
        fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
    except BlockingIOError:
        lock.seek(0)
        log("Waiting for the shared ONI install: " + lock.read())
        fcntl.flock(lock, fcntl.LOCK_EX)
    lock.seek(0); lock.truncate()
    json.dump({"pid": os.getpid(), "project": str(ROOT), "since": datetime.now(timezone.utc).isoformat()}, lock)
    lock.flush()
    try:
        if game_running():
            raise SystemExit("ONI is running without a lease; not touching it.")
        if PENDING.exists():
            log("Restoring the profile left by an interrupted session first")
            restore()
        saved = RUN / f"profile-{datetime.now():%Y%m%dT%H%M%S}"
        snapshot(saved)
        try:
            install(saved)
            shutil.copy2(save, RUN / save.name)
            for name in ("command.json", "result.json"):
                (RUN / name).unlink(missing_ok=True)
            game = subprocess.Popen([str(PLAYER), "-logFile", str(RUN / "player.log"), "-screen-width", "1600",
                "-screen-height", "900", "-screen-fullscreen", "0"], env=dict(os.environ, SteamAppId="457140",
                SteamGameId="457140"), stdin=subprocess.DEVNULL, stdout=subprocess.DEVNULL,
                stderr=subprocess.DEVNULL, start_new_session=True)
            (RUN / "session.json").write_text(json.dumps({"pid": game.pid, "save": save.name, "profile": saved.name}))
            log(f"ONI pid {game.pid}; drive it with tests/live-command.sh, save basename {save.name}")
            deadline = time.monotonic() + minutes * 60
            while game.poll() is None and time.monotonic() < deadline and not stop:
                time.sleep(2)
        finally:
            quit_and_wait()
            restore()
    finally:
        lock.seek(0); lock.truncate(); lock.flush()
        fcntl.flock(lock, fcntl.LOCK_UN)
        lock.close()


if __name__ == "__main__":
    main()
