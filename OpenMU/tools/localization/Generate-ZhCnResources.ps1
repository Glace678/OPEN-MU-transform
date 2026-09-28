param(
    [string]$RepositoryRoot = (Join-Path $PSScriptRoot "..\.."),
    [string]$TranslationFile = (Join-Path $PSScriptRoot "zh-cn-translations.json")
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$translations = Get-Content -LiteralPath $TranslationFile -Raw | ConvertFrom-Json -AsHashtable
$utf8WithoutBom = [System.Text.UTF8Encoding]::new($false)
$writerSettings = [System.Xml.XmlWriterSettings]::new()
$writerSettings.Encoding = $utf8WithoutBom
$writerSettings.Indent = $true
$writerSettings.NewLineChars = "`n"
$writerSettings.NewLineHandling = [System.Xml.NewLineHandling]::Replace

function Get-Placeholders([string]$value) {
    $indexed = [regex]::Matches($value, "(?<!\{)\{\d+(?:[^}]*)?\}(?!\})") | ForEach-Object Value
    $printf = [regex]::Matches(
        $value.Replace("%%", ""),
        "%[-+ #0]*(?:\d+|\*)?(?:\.(?:\d+|\*))?(?:hh|h|ll|l|j|z|t|L|I32|I64)?[diuoxXfFeEgGaAcspn]") | ForEach-Object Value
    return @(($indexed + $printf) | Sort-Object)
}

foreach ($relativeSource in $translations.Keys) {
    $definition = $translations[$relativeSource]
    $sourcePath = Join-Path $RepositoryRoot $relativeSource
    $destinationPath = $sourcePath -replace '\.resx$', '.zh-CN.resx'
    [xml]$resource = Get-Content -LiteralPath $sourcePath -Raw

    $sourceEntries = @($resource.root.data)
    $sourceKeys = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($entry in $sourceEntries) {
        [void]$sourceKeys.Add([string]$entry.name)
    }

    if ($definition.ContainsKey("ValueTranslationsFile")) {
        $valueTranslationPath = Join-Path $PSScriptRoot ([string]$definition.ValueTranslationsFile)
        $valueTranslations = Get-Content -LiteralPath $valueTranslationPath -Raw | ConvertFrom-Json -AsHashtable
        $translatedValues = @{}
        $missingValues = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
        foreach ($entry in $sourceEntries) {
            $sourceValue = [string]$entry.value
            if ([string]::IsNullOrWhiteSpace($sourceValue)) {
                continue
            }

            if ($valueTranslations.ContainsKey($sourceValue)) {
                $translatedValues[[string]$entry.name] = [string]$valueTranslations[$sourceValue]
            }
            else {
                [void]$missingValues.Add($sourceValue)
            }
        }

        if ($missingValues.Count -ne 0) {
            throw "$relativeSource has untranslated values: $($missingValues -join ', ')"
        }
    }
    else {
        $translatedValues = $definition.Translations
    }
    $unknownKeys = @($translatedValues.Keys | Where-Object { -not $sourceKeys.Contains($_) })
    if ($unknownKeys.Count -ne 0) {
        throw "$relativeSource contains unknown translation keys: $($unknownKeys -join ', ')"
    }

    $missingKeys = @($sourceKeys | Where-Object { -not $translatedValues.ContainsKey($_) })
    if ($definition.RequireComplete -and $missingKeys.Count -ne 0) {
        throw "$relativeSource is missing translations: $($missingKeys -join ', ')"
    }

    foreach ($entry in $sourceEntries) {
        $key = [string]$entry.name
        if (-not $translatedValues.ContainsKey($key)) {
            [void]$entry.ParentNode.RemoveChild($entry)
            continue
        }

        $translated = [string]$translatedValues[$key]
        if ([string]::IsNullOrWhiteSpace($translated)) {
            throw "$relativeSource translation '$key' is empty."
        }

        $sourcePlaceholders = @(Get-Placeholders ([string]$entry.value))
        $translatedPlaceholders = @(Get-Placeholders $translated)
        if (($sourcePlaceholders -join "|") -cne ($translatedPlaceholders -join "|")) {
            throw "$relativeSource translation '$key' changed placeholders."
        }

        $entry.value = $translated
    }

    $writer = [System.Xml.XmlWriter]::Create($destinationPath, $writerSettings)
    try {
        $resource.Save($writer)
    }
    finally {
        $writer.Dispose()
    }

    Write-Output "Generated $destinationPath ($($translatedValues.Count) translated entries)."
}
