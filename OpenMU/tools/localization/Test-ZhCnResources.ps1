param(
    [string]$RepositoryRoot = (Join-Path $PSScriptRoot "..\.."),
    [string]$TranslationFile = (Join-Path $PSScriptRoot "zh-cn-translations.json")
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$definitions = Get-Content -LiteralPath $TranslationFile -Raw | ConvertFrom-Json -AsHashtable
$bannedTerms = @(
    "网路", "伺服器", "设定", "档案", "资料夹", "帐号", "帐户", "身分",
    "滑鼠", "游标", "预设", "介面", "连线", "程式", "透过", "卷动",
    "侦测", "安装档", "储值", "水准", "混沌城堡", "冷狼要塞", "公会",
    "行会", "派对"
)
$mojibakeMarkers = @("�", "Ã", "Â", "â€", "ï¿½")

function Get-Placeholders([string]$value) {
    $indexed = [regex]::Matches($value, "(?<!\{)\{\d+(?:[^}]*)?\}(?!\})") | ForEach-Object Value
    $printf = [regex]::Matches(
        $value.Replace("%%", ""),
        "%[-+ #0]*(?:\d+|\*)?(?:\.(?:\d+|\*))?(?:hh|h|ll|l|j|z|t|L|I32|I64)?[diuoxXfFeEgGaAcspn]") | ForEach-Object Value
    return @(($indexed + $printf) | Sort-Object)
}

function Get-LogicalLineBreakCount([string]$value) {
    $count = 0
    for ($index = 0; $index -lt $value.Length; $index++) {
        if ($value[$index] -eq "`r") {
            if ($index + 1 -lt $value.Length -and $value[$index + 1] -eq "`n") {
                $index++
            }

            $count++
        }
        elseif ($value[$index] -eq "`n") {
            $count++
        }
        elseif ($value[$index] -eq '\' -and $index + 1 -lt $value.Length -and $value[$index + 1] -eq 'n') {
            $index++
            $count++
        }
    }

    return $count
}

foreach ($relativeSource in $definitions.Keys) {
    $definition = $definitions[$relativeSource]
    $sourcePath = Join-Path $RepositoryRoot $relativeSource
    $targetPath = $sourcePath -replace '\.resx$', '.zh-CN.resx'
    [xml]$source = Get-Content -LiteralPath $sourcePath -Raw
    [xml]$target = Get-Content -LiteralPath $targetPath -Raw

    $sourceByKey = @{}
    foreach ($entry in @($source.root.data)) {
        $sourceByKey[[string]$entry.name] = [string]$entry.value
    }

    if ($definition.ContainsKey("ValueTranslationsFile")) {
        $expectedKeys = @($sourceByKey.Keys | Where-Object {
            -not [string]::IsNullOrWhiteSpace([string]$sourceByKey[$_])
        })
    }
    else {
        $expectedKeys = @($definition.Translations.Keys)
    }

    $targetByKey = @{}
    foreach ($entry in @($target.root.data)) {
        $key = [string]$entry.name
        if ($targetByKey.ContainsKey($key)) {
            throw "$targetPath contains duplicate key '$key'."
        }

        $targetByKey[$key] = [string]$entry.value
    }

    $missingKeys = @($expectedKeys | Where-Object { -not $targetByKey.ContainsKey($_) })
    $unexpectedKeys = @($targetByKey.Keys | Where-Object { $_ -notin $expectedKeys })
    if ($missingKeys.Count -ne 0 -or $unexpectedKeys.Count -ne 0) {
        throw "$targetPath key mismatch. Missing: $($missingKeys -join ', '); unexpected: $($unexpectedKeys -join ', ')."
    }

    foreach ($key in $targetByKey.Keys) {
        $value = [string]$targetByKey[$key]
        if ([string]::IsNullOrWhiteSpace($value)) {
            throw "$targetPath translation '$key' is empty."
        }

        $sourcePlaceholders = @(Get-Placeholders ([string]$sourceByKey[$key]))
        $targetPlaceholders = @(Get-Placeholders $value)
        if (($sourcePlaceholders -join "|") -cne ($targetPlaceholders -join "|")) {
            throw "$targetPath translation '$key' changed placeholders."
        }

        if ((Get-LogicalLineBreakCount ([string]$sourceByKey[$key])) -ne (Get-LogicalLineBreakCount $value)) {
            throw "$targetPath translation '$key' changed line break count."
        }

        foreach ($term in $bannedTerms) {
            if ($value.Contains($term, [StringComparison]::Ordinal)) {
                throw "$targetPath translation '$key' contains Taiwan-only term '$term'."
            }
        }

        foreach ($marker in $mojibakeMarkers) {
            if ($value.Contains($marker, [StringComparison]::Ordinal)) {
                throw "$targetPath translation '$key' contains mojibake marker '$marker'."
            }
        }
    }

    Write-Output "Audited $targetPath ($($targetByKey.Count) translated entries)."
}
