# Card design reference

Offline reference for the collectible map export, drawn from a LiveProbe run of the neutral save
(`dump`, `assets` and World-preview render exports). `mock6.py` is the approved design and imports
the earlier passes for terrain colouring and helpers. The run folders hold Klei's sprites and fonts,
so they stay in ignored `artifacts/` and never enter the repository.

    uv run --with pillow --with numpy design/cards/mock6.py RUN_DIR OUT_DIR

`RUN_DIR` needs `dump.json`, `dump-*.bin`, `assets/` and `renders/` (one World-preview export per
world). It reads the game's `NotoSansUI-Regular.ttf` from `design/cards/fonts/` (copy it from
the game's StreamingAssets/fonts; not committed). The design itself is in `docs/directions.md`.
