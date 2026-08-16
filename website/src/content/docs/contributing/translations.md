---
title: Contributing translations
description: Correct existing SRS translations or add a new language safely.
---

Client translations are stored in `IL2-SR-Client/Localization/*.resx`. Community corrections are welcome, especially where machine-translated text is unclear or too long for the interface.

## Suggest a correction

Open a [Translation correction issue](https://github.com/riaanjutte/IL2-SimpleRadioStandalone/issues/new?template=translation-correction.yml) and include:

- the language
- the current text
- the suggested replacement
- where the text appears in the client

## Edit a translation

1. Open the relevant `.resx` file.
2. Find the matching `<data name="...">` entry.
3. Keep the `name` unchanged.
4. Edit only the text inside `<value>...</value>`.
5. Open a pull request.

```xml
<data name="Connect" xml:space="preserve">
  <value>Conectar</value>
</data>
```

Keep placeholders such as `{0}` unchanged, preserve XML escaping, and keep button labels concise enough for the WPF interface.

## Validate locally

```powershell
pwsh ./scripts/Validate-ResxTranslations.ps1
```

Translation pull requests run the same checks automatically. The validator catches invalid XML, missing or unknown keys, duplicate keys, blank values, and broken placeholders.

See [TRANSLATING.md](https://github.com/riaanjutte/IL2-SimpleRadioStandalone/blob/master/TRANSLATING.md) for the complete contribution workflow.
