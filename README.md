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

Latest package: **0.4.1.0**. Workshop update is pending; its last verified release is **0.3.1.0**.
46 managed checks and 26 final native assertions pass, including actual Shift+F7/F8
input, planet buttons, camera restoration, save/reload and permanent rocket-route analysis.
Testing used the original mod profile, including Bigger Camera Zoom Out, on macOS
with ONI U59-744825. Native launch prerequisites remain enforced; this fixture's
rockets were not launch-ready, so a full flight was not exercised.
See [validation status](docs/progress.md).

Upload the complete `dist/liyux.RevealStarMap` folder. The build also creates a
versioned ZIP with the same content.

[Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3695988693)
and [GitHub releases](https://github.com/Liyux3/reveal-starmap/releases).

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

