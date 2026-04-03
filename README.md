# Reveal StarMap

Independent `Oxygen Not Included` mod for revealing the entire `Spaced Out` starmap on demand.

## Current Scope

- configurable hotkey through PLib options
- instant full starmap reveal
- second press restores the last pre-reveal fog-of-war snapshot
- restore snapshot is persisted in the save, so a save-load cycle can still roll back a temporary reveal
- a world side-screen button can fully reveal one selected asteroid map for debug inspection, then restore it
- a second configurable hotkey can reveal the entire live simulation canvas across all worlds, then restore it
- lightweight runtime notification

## Default Hotkey

- `Shift + F7`, press once to reveal, press again to restore
- `Shift + F8`, press once to reveal the full live canvas, press again to restore

## Build

1. `./scripts/bootstrap-dotnet.sh`
2. `./scripts/install-local.sh`
