# Community translations

The client loads translations from the `.resx` files in this folder. These files are intended to be easy for the community to edit without changing C# code or rebuilding the app.

For the full contributor workflow, see `../../TRANSLATING.md`.

If you do not want to edit files directly, open a GitHub issue using the `Translation correction` template.

## Updating a translation

1. Open the language file, for example `es.resx`.
2. Keep each `name` value exactly as it is. These are the English lookup keys used by the client.
3. Edit only the text inside the matching `<value>` element.
4. Save the file as UTF-8.
5. Restart the client to apply the change.

Example:

```xml
<data name="Connect" xml:space="preserve">
  <value>Conectar</value>
</data>
```

## Adding a language

1. Copy `en.resx` to a new file named with the two-letter language code, for example `it.resx`.
2. Translate the `<value>` text for each entry.
3. Build or run the client. The language picker will include the new file automatically.

The language picker uses the file name as the language code. For built-in languages, the existing display names are preserved. For new languages, the client uses the culture's native display name when Windows recognizes the code.

If a translation is missing or invalid, the client falls back to English for that text.

## Validation

Translation pull requests are checked automatically. You can run the same validation locally from the repository root:

```powershell
pwsh ./scripts/Validate-ResxTranslations.ps1
```

On Windows PowerShell:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Validate-ResxTranslations.ps1
```
