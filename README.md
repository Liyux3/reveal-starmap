# Reveal StarMap

Temporary map previews for **Oxygen Not Included: Spaced Out!**

- **Shift + F7** previews the starmap. Press again to return.
- **Shift + F8** previews the canvas containing all worlds. Press again to return.
- Select an asteroid and use **Preview map** or **End preview** in its details.

Previews preserve real exploration progress. World and canvas previews restore your
previous camera view when ended and close when reloading a save.

## Configure Once

Open **Mods > Reveal StarMap > Options** from the game's main menu.
Shortcuts, language and behavior are shared across all saves and apply when saved.

Defaults are reversible preview, the game's language and silent operation.
English, Simplified Chinese and Traditional Chinese are included. Routine action
notifications can be enabled in Preferences; actionable errors remain visible.

An optional **Starmap > Action > Permanent survey** setting makes the starmap shortcut
permanently analyze the current map without another prompt. Its tooltip explains
permanence, telescope rewards and navigation limits. Changing a setting or loading a
save never performs analysis by itself. Ordinary preview controls stay the same.

For details, see [behavior and data](docs/architecture.md) and
[translation guidance](docs/localization.md).

## Release Status

Packaged locally: **0.4.0.0**. Workshop: **0.3.1.0**.
Managed checks and live game integration pass, including options save/reopen, preview
restoration, native rocket route validation and save/reload. Physical input automation
remains unreliable, and a complete launch-and-arrival flight has not been exercised.
See [validation status](docs/progress.md).

Upload the complete mod folder, including its `translations` directory.

## Development

```sh
./scripts/bootstrap-dotnet.sh
./scripts/build.sh
.tooling/dotnet/dotnet run --project tests/Verify.csproj -- \
  "$HOME/Library/Application Support/Steam/steamapps/common/OxygenNotIncluded/OxygenNotIncluded.app/Contents/Resources/Data/Managed"
```

The verifier uses the built DLL, actual PLib and ONI managed types. It covers analysis
preservation, serialization, settings migration, typed option write-back and all
translation catalogs. Live rendering, native save loading and flight require the game.

`./scripts/install-local.sh` installs a local copy. Enable only one Local or Workshop
copy with the same mod ID while testing.

The preview image is based on an official Spaced Out Steam store screenshot.
