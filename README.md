# Reveal StarMap

Temporary map previews for **Oxygen Not Included: Spaced Out!** Look at everything,
then put it back.

- **Shift + F7** previews the starmap. Press again to return.
- **Shift + F8** previews the canvas containing all worlds. Press again to return.
- **Shift + F9** saves a framed PNG of what you are viewing: the previewed asteroid, the
  whole canvas, or the current asteroid as explored, captioned with its name, cycle and seed.
- Select an asteroid and use **Preview map** or **End preview** in its details.

Exports go to `RevealStarMap/exports` in the game's data folder, next to `save_files`.

Previews change only what is drawn. Exploration, research, telescope rewards and
achievements stay as they were, and hidden areas do not start simulating, unlike debug
mode. World and canvas previews restore your previous camera view when ended, end when
you switch to another world, and close when reloading a save.

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

Latest package: [**0.4.2.0**](https://github.com/Liyux3/reveal-starmap/releases/tag/v0.4.2.0).
Workshop installation is currently affected by an upload-format issue. Use the
`RevealStarMap-0.4.2.0.zip` download for manual installation while the original
[Workshop listing](https://steamcommunity.com/sharedfiles/filedetails/?id=3695988693)
is restored. Existing subscriptions stay attached to that listing.

0.4.2 passed 50 managed checks and native acceptance of world switches during previews,
saved-view restoration, camera bounds and the merged PLib options dialog.
0.4.1 passed 48 managed checks and native acceptance on macOS with ONI U59-744825:
actual Shift+F7/F8 input, planet buttons, camera restoration, save/reload, permanent
rocket-route analysis and a real rocket flight, using the original mod profile.
See [validation status](docs/progress.md).

## Development

```sh
./scripts/bootstrap-dotnet.sh
./scripts/check.sh
```

An installed .NET SDK can be used directly; bootstrap is needed only when none is available.
Set `ONI_MANAGED_PATH` for a nondefault game installation. The build merges the pinned
PLib in `vendor` into the mod assembly, so no other mod's PLib can replace it, embeds all
three languages and creates the upload folder and ZIP together.
Previous generated folders are moved to the system Trash.

The build creates `dist/liyux.RevealStarMap`, a Local-install ZIP containing that
folder and a separate `-workshop.zip` with package files at its root.

ONI's Steam installer expects a legacy ZIP file, not a modern Workshop depot directory.
Release in this order: raise the version, run `./scripts/check.sh`, tag and publish the
GitHub release, then run `./scripts/publish-workshop.sh VERSION NOTE_FILE`. It sends the
release's Workshop ZIP to item `3695988693` through the official uploader's legacy
contract, changes only the file and change note, and verifies a subscriber install.
Keep the Workshop description user-authored; edit it on Steam.

The verifier uses the shipped DLL, its merged PLib and ONI managed types. It covers analysis
preservation, serialization, settings migration, typed option write-back and all
translation catalogs, legacy exploration coordinates and archive contents.
Live rendering, native save loading and flight require the game.

`./scripts/install-local.sh` installs a local copy. Enable only one Local or Workshop
copy with the same mod ID while testing. For in-game checks, build the driver with
`tests/build-live-probe.sh` and run `tests/native-session.py SAVE`, which shares the
game install with other local projects through a lock and restores your mod profile,
preferences and config afterwards; [tests/README.md](tests/README.md) lists the commands.

