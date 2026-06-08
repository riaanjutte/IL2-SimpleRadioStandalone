# Helping translate IL2-SRS Community Edition

The client UI translations are stored in `.resx` files:

- English source: `IL2-SR-Client/Localization/en.resx`
- German: `IL2-SR-Client/Localization/de.resx`
- French: `IL2-SR-Client/Localization/fr.resx`
- Spanish: `IL2-SR-Client/Localization/es.resx`
- Italian: `IL2-SR-Client/Localization/it.resx`
- Russian: `IL2-SR-Client/Localization/ru.resx`

The non-English translations are machine translated and community corrections are welcome.

## Easiest option: suggest a correction

If you do not want to edit files, open a GitHub issue using the `Translation correction` template and include:

- the language
- the current text
- the suggested replacement
- where the text appears in the app

## Editing a translation file

1. Open the language `.resx` file you want to improve.
2. Find the matching `<data name="...">` entry.
3. Keep the `name` value unchanged.
4. Edit only the text inside `<value>...</value>`.
5. Open a pull request with the change.

Example:

```xml
<data name="Connect" xml:space="preserve">
  <value>Conectar</value>
</data>
```

## Rules for safe changes

- Do not rename, remove, or add `name` keys unless the source English file is also being updated.
- Keep placeholders unchanged. If English has `{0}`, the translation must also contain `{0}`.
- Keep button labels short where possible. Long text can clip in the WPF UI.
- Preserve XML escaping, for example use `&amp;` for `&`.
- Save files as UTF-8.

## Adding a language

1. Copy `IL2-SR-Client/Localization/en.resx`.
2. Rename the copy to a two-letter language code, for example `it.resx`.
3. Translate the `<value>` text.
4. Open a pull request.

The app discovers `.resx` language files at startup. Restart the client after changing translation files.

## Validation

Translation pull requests are checked by GitHub Actions. The validator fails if:

- a `.resx` file is invalid XML
- a key is missing
- a key is unknown
- a key is duplicated
- a value is blank
- placeholders such as `{0}` do not match English

You can run the same check locally:

```powershell
pwsh ./scripts/Validate-ResxTranslations.ps1
```

On Windows PowerShell:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Validate-ResxTranslations.ps1
```
