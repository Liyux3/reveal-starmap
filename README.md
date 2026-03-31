# Reveal StarMap

Independent `Oxygen Not Included` mod for revealing the entire `Spaced Out` starmap on demand.

## Current Scope

- configurable hotkey through PLib options
- instant full starmap reveal
- second press restores the last pre-reveal fog-of-war snapshot
- restore snapshot is persisted in the save, so a save-load cycle can still roll back a temporary reveal
- lightweight runtime notification

## Default Hotkey

- `Shift + F7`, press once to reveal, press again to restore

## Build

1. `./scripts/bootstrap-dotnet.sh`
2. `./scripts/install-local.sh`

The built mod installs to:

- `/Users/liyux/Library/Application Support/unity.Klei.Oxygen Not Included/mods/Local/liyux.RevealStarMap`


## Preview source

- Based on an official Steam store screenshot for `Oxygen Not Included: Spaced Out`.
