# Localization and Copy

All mod-provided settings labels, choices, tooltips, button text, routine notifications
and error text live in `translations/*.json`. Internal diagnostic logs remain English.

- `en.json`: canonical keys and final fallback, also shipped as a translator reference.
- `zh-CN.json`: Simplified Chinese, including the game's plain `zh` locale.
- `zh-TW.json`: Traditional Chinese, including `zh-Hant`, `zh-HK` and `zh-MO` variants.

Follow game is the default. Explicit language preference affects mod text; native PLib
and game window controls continue using the game language. Reopen options after changing
language. Existing notifications and world buttons use the selected language when refreshed.

To add a language, copy `en.json` to a locale-named catalog such as `de.json`, translate
values and keep keys and `{0}` placeholders intact. Follow game tries the exact code and
then its language prefix. Chinese script subtags take precedence over region. All three
shipped languages are also embedded, so a missing or unreadable external catalog retains
its matching language. Missing or empty keys fall back to English; invalid format strings fall back when formatted. Locale filenames cannot contain
path separators. Catalogs are cached for the session; restart after editing translation files.

Options use stable internal category keys so Starmap, Canvas and Preferences stay in
the same order in every language. The native string table supplies their display labels.

Use short verbs for buttons: Preview map and End preview. Preview means
reversible visual access; survey/analysis means persistent navigation state. In Chinese,
use 预览/預覽, 分析, 星图/星圖 and 画布/畫布 consistently. Put technical explanations in
tooltips and documentation. Permanent-survey consequences belong in the mode option's
tooltip; ordinary previews have no navigation-analysis messaging or onboarding.

The managed verifier checks catalog coverage, placeholders, locale aliases, fallback,
typed option write-back and refreshed option language. Live Unity layout remains a separate
visual test. Upload the complete package, including its `translations` directory.
