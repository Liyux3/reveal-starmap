# Reveal StarMap

Independent `Oxygen Not Included: Spaced Out` mod for temporary map previews and optional permanent starmap analysis.

`0.4.0.0` is a local release candidate. The published Workshop version is `0.3.1.0`.

## Choose Your Behavior

In **Mod Options > Star Map Action**, choose:

| Action | What happens | Rocket navigation | Reversible |
| --- | --- | --- | --- |
| Temporary Preview (default) | Displays the full starmap while preserving actual analysis | Normal analysis requirements | Press the hotkey again |
| Permanent Survey | The hotkey asks for confirmation, then analyzes existing starmap cells in this save | Uses real analyzed cells; fuel, range and landing requirements still apply | Load a save from before confirmation |

Changing the setting or loading a save never performs a permanent survey. Canceling
the confirmation leaves exploration untouched. After surveying, removing the mod or
switching back to preview does not erase analysis. Later cluster expansion requires
another explicit survey. An active preview can always be ended with the same key,
including after changing the option.

World and full-canvas previews remain temporary in both modes.

### Achievements and Research

Permanent Survey keeps debug, instant-build and sandbox flags unchanged, so it does
not itself disable Steam achievements or re-enable previously disabled achievements.
It changes exploration progression, and other mods may add their own achievement rules.
It does not mark planets as visited or reveal their terrain. The native terrain-reveal
and landing achievements therefore still use their original requirements.

Surveying bypasses telescope analysis **and its Data Bank rewards**. Use normal
exploration if you want that research progression. Temporary Preview preserves the
real analysis state and ongoing research progress.

## Current Scope

- configurable hotkey through PLib options
- configurable preview/survey behavior with English and Chinese confirmation text
- instant full starmap preview without changing exploration progress
- second press ends the preview and returns to the normal fog-of-war view
- a world side-screen button previews one selected asteroid without triggering discovery or hidden spawns
- a second configurable hotkey previews the entire live simulation canvas across all worlds
- legacy snapshots from older releases are restored once and migrated to the render-only preview model
- only one preview mode is active at a time, with camera state restored after world and canvas previews
- world and canvas previews end safely when a save is reloaded because camera snapshots are session-local
- one introductory notification per save, then silent preview toggles; permanent survey always asks for confirmation

## Default Hotkey

- `Shift + F7`, press once to preview the starmap, press again to end the preview
- `Shift + F8`, press once to preview the full live canvas, press again to end the preview

In Permanent Survey mode, `Shift + F7` opens the analysis confirmation instead.
All bindings remain configurable. Settings apply when confirmed without restarting.
Hotkeys are ignored in modal dialogs and text fields, and modifier matching is exact.

## Validation

Build against your installed game and run the managed integration checks:

```sh
./scripts/build.sh
.tooling/dotnet/dotnet run --project tests/Verify.csproj -- \
  "$HOME/Library/Application Support/Steam/steamapps/common/OxygenNotIncluded/OxygenNotIncluded.app/Contents/Resources/Data/Managed"
```

These checks load the shipped mod, real PLib and ONI managed types. They cover
analysis preparation, preservation, idempotence, serialization and options write-back.
They do not execute Unity rendering, native save loading or live rocket flight.
See [architecture](docs/architecture.md) and [validation status](docs/progress.md).

## Build

1. `./scripts/bootstrap-dotnet.sh`
2. `./scripts/install-local.sh`

The built mod installs to:

- `/Users/liyux/Library/Application Support/unity.Klei.Oxygen Not Included/mods/Local/liyux.RevealStarMap`


## Preview source

- Based on an official Steam store screenshot for `Oxygen Not Included: Spaced Out`.
