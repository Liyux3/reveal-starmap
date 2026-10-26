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

Local release: **0.4.1.0**. Last verified Workshop release: **0.3.1.0**.
The local release has not been uploaded to Workshop.
46 managed checks and targeted native regressions pass, including options save/reopen,
rapid preview transitions, camera restoration and zoom limits with Bigger Camera Zoom Out.
Earlier integration covered native rocket routes and save/reload. System keyboard
coverage is partial, and a complete launch-and-arrival flight has not been exercised.
See [validation status](docs/progress.md).

Upload the complete `dist/liyux.RevealStarMap` folder. The build also creates a
versioned ZIP with the same content.

## Development

```sh
./scripts/bootstrap-dotnet.sh
./scripts/check.sh
```

An installed .NET SDK can be used directly; bootstrap is needed only when none is available.
Set `ONI_MANAGED_PATH` for a nondefault game installation. The build uses the pinned
PLib in `vendor`, embeds all three languages and creates the upload folder and ZIP together.
Previous generated folders are moved to the system Trash.

The verifier uses the built DLL, actual PLib and ONI managed types. It covers analysis
preservation, serialization, settings migration, typed option write-back and all
translation catalogs, legacy exploration coordinates and archive contents.
Live rendering, native save loading and flight require the game.

`./scripts/install-local.sh` installs a local copy. Enable only one Local or Workshop
copy with the same mod ID while testing.

The preview image is based on an official Spaced Out Steam store screenshot.
