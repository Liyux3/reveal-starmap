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

Latest package: [**0.4.1.0**](https://github.com/Liyux3/reveal-starmap/releases/tag/v0.4.1.0).
Workshop installation is currently affected by an upload-format issue. Use the
`RevealStarMap-0.4.1.0.zip` download for manual installation while the original
[Workshop listing](https://steamcommunity.com/sharedfiles/filedetails/?id=3695988693)
awaits storage recovery. Existing subscriptions stay attached to that listing.

48 managed checks and 26 final native assertions pass, including actual Shift+F7/F8
input, planet buttons, camera restoration, save/reload and permanent rocket-route analysis.
Testing used the original mod profile, including Bigger Camera Zoom Out, on macOS
with ONI U59-744825. Native launch prerequisites remain enforced; this fixture's
rockets were not launch-ready, so a full flight was not exercised.
See [validation status](docs/progress.md).

## Development

```sh
./scripts/bootstrap-dotnet.sh
./scripts/check.sh
```

An installed .NET SDK can be used directly; bootstrap is needed only when none is available.
Set `ONI_MANAGED_PATH` for a nondefault game installation. The build uses the pinned
PLib in `vendor`, embeds all three languages and creates the upload folder and ZIP together.
Previous generated folders are moved to the system Trash.

The build creates `dist/liyux.RevealStarMap`, a Local-install ZIP containing that
folder and a separate `-workshop.zip` with package files at its root.

ONI's current Steam installer expects a legacy ZIP file, rather than a modern Workshop
depot directory. Use the official ONI Mod Uploader for the folder, or an explicitly
legacy upload of the root-level Workshop ZIP. Validate that Steam's GetItemInstallInfo
returns a regular ZIP file before accepting a release. The unsafe SteamCMD VDF has
been retired. Publisher app 636750 and consumer app 457140 are distinct contexts.
The existing Workshop item `3695988693` is the sole publication target. Its conversion
to modern storage has not been reversed by the available client APIs; retain the
listing and its installation notice until platform recovery is verified.

The verifier uses the built DLL, actual PLib and ONI managed types. It covers analysis
preservation, serialization, settings migration, typed option write-back and all
translation catalogs, legacy exploration coordinates and archive contents.
Live rendering, native save loading and flight require the game.

`./scripts/install-local.sh` installs a local copy. Enable only one Local or Workshop
copy with the same mod ID while testing.

