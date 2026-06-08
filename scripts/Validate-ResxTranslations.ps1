param(
    [string]$LocalizationPath = "IL2-SR-Client/Localization",
    [string]$BaseLanguage = "en"
)

$ErrorActionPreference = "Stop"
$failures = New-Object System.Collections.Generic.List[string]

function Add-Failure {
    param([string]$Message)
    $failures.Add($Message) | Out-Null
}

function Get-PlaceholderSignature {
    param([string]$Text)

    if ($null -eq $Text) {
        return ""
    }

    $matches = [regex]::Matches($Text, "(?<!\{)\{[0-9]+(?::[^{}]*)?\}(?!\})")
    if ($matches.Count -eq 0) {
        return ""
    }

    return ($matches |
        ForEach-Object { $_.Value } |
        Group-Object |
        Sort-Object Name |
        ForEach-Object { "$($_.Name)=$($_.Count)" }) -join "|"
}

function Read-Resx {
    param([string]$Path)

    try {
        [xml]$document = Get-Content -LiteralPath $Path -Raw -Encoding UTF8
    }
    catch {
        Add-Failure "$Path is not valid XML: $($_.Exception.Message)"
        return @{}
    }

    if ($null -eq $document.root) {
        Add-Failure "$Path does not contain a root element"
        return @{}
    }

    $nodes = @($document.root.data)
    $map = New-Object "System.Collections.Generic.Dictionary[string,string]" ([System.StringComparer]::Ordinal)

    foreach ($node in $nodes) {
        $name = [string]$node.name
        if ([string]::IsNullOrWhiteSpace($name)) {
            Add-Failure "$Path contains a <data> entry without a name"
            continue
        }

        if ($map.ContainsKey($name)) {
            Add-Failure "$Path contains duplicate key '$name'"
        }
        else {
            $map[$name] = [string]$node.value
        }
    }

    return $map
}

$resolvedLocalizationPath = Resolve-Path -LiteralPath $LocalizationPath
$baseFile = Join-Path $resolvedLocalizationPath "$BaseLanguage.resx"

if (-not (Test-Path -LiteralPath $baseFile)) {
    throw "Base language file not found: $baseFile"
}

$base = Read-Resx -Path $baseFile
$baseKeys = @($base.Keys)

if ($baseKeys.Count -eq 0) {
    Add-Failure "$baseFile does not contain any translatable <data> entries"
}

$translationFiles = Get-ChildItem -LiteralPath $resolvedLocalizationPath -Filter "*.resx" |
    Sort-Object Name

foreach ($file in $translationFiles) {
    $translations = Read-Resx -Path $file.FullName
    $translationKeys = @($translations.Keys)

    $missingKeys = $baseKeys | Where-Object { -not $translations.ContainsKey($_) }
    foreach ($key in $missingKeys) {
        Add-Failure "$($file.FullName) is missing key '$key'"
    }

    $extraKeys = $translationKeys | Where-Object { -not $base.ContainsKey($_) }
    foreach ($key in $extraKeys) {
        Add-Failure "$($file.FullName) contains unknown key '$key'"
    }

    foreach ($key in $baseKeys) {
        if (-not $translations.ContainsKey($key)) {
            continue
        }

        $sourceValue = $base[$key]
        $translatedValue = $translations[$key]

        if (-not [string]::IsNullOrWhiteSpace($sourceValue) -and [string]::IsNullOrWhiteSpace($translatedValue)) {
            Add-Failure "$($file.FullName) has an empty value for key '$key'"
        }

        $sourcePlaceholders = Get-PlaceholderSignature -Text $sourceValue
        $translatedPlaceholders = Get-PlaceholderSignature -Text $translatedValue

        if ($sourcePlaceholders -ne $translatedPlaceholders) {
            Add-Failure "$($file.FullName) placeholder mismatch for key '$key'. Expected '$sourcePlaceholders', found '$translatedPlaceholders'"
        }
    }
}

if ($failures.Count -gt 0) {
    foreach ($failure in $failures) {
        Write-Host "::error::$failure"
    }

    throw "RESX validation failed with $($failures.Count) issue(s)."
}

Write-Host "RESX validation passed for $($translationFiles.Count) file(s)."
