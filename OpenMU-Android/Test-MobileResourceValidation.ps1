[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'MobileResourceValidation.ps1')

function Assert-ResourceRejection {
    param([scriptblock]$Action, [string]$Message)

    try {
        & $Action
    } catch {
        if ($_.Exception.Message.Contains($Message)) { return }
        throw
    }
    throw "Resource validation did not reject: $Message"
}

function New-TestResourceCatalog {
    param([string]$Xml)

    $catalog = [Collections.Generic.Dictionary[string, Xml.XmlElement]]::new([StringComparer]::Ordinal)
    Add-MobileResourceEntries ([xml]$Xml) $catalog 'fixture'
    return ,$catalog
}

$checks = @(
    { Test-MobileResourceText 'Screen: %1$d x %2$d' 'Bildschirm: %1$d x %2$d' 'valid' },
    { Test-MobileResourceText 'A\nB\n' 'C\nD\n' 'valid line breaks' },
    { Test-MobileResourceText 'Complete: %%' 'Fertig: %%' 'valid percent' },
    { Test-MobileResourceText 'Text' 'L\''application' 'valid quote' },
    { Assert-ResourceRejection { Test-MobileResourceText '%1$s' '%1$d' 'type' } 'Format placeholder' },
    { Assert-ResourceRejection { Test-MobileResourceText '%1$d' '%2$d' 'index' } 'Format placeholder' },
    { Assert-ResourceRejection { Test-MobileResourceText '%1$d %1$d' '%1$d' 'repeated' } 'Format placeholder' },
    { Assert-ResourceRejection { Test-MobileResourceText 'Text' '100%' 'literal' } 'Format placeholder' },
    { Assert-ResourceRejection { Test-MobileResourceText 'Text' "L'application" 'quote' } 'Unescaped Android quote' },
    { Assert-ResourceRejection { Test-MobileResourceText 'Text' 'bad\q' 'escape' } 'Invalid Android escape' },
    { Assert-ResourceRejection { Test-MobileResourceText 'A\nB' 'AB' 'line' } 'Line break mismatch' },
    { Assert-ResourceRejection { Test-MobileResourceText 'Text' ' ' 'empty' } 'Empty resource' },
    {
        $reference = New-TestResourceCatalog '<resources><string name="key">Text</string></resources>'
        $localized = New-TestResourceCatalog '<resources><string name="other">Text</string></resources>'
        Assert-ResourceRejection { Test-MobileResourceCatalog $reference $localized 'missing' } 'Missing resource'
    },
    {
        Assert-ResourceRejection {
            New-TestResourceCatalog '<resources><string name="key">A</string><string name="key">B</string></resources>'
        } 'duplicate resource name'
    },
    {
        Assert-ResourceRejection {
            New-TestResourceCatalog '<resources><string name="key" translatable="false">A</string></resources>'
        } 'validation is disabled'
    },
    {
        $reference = New-TestResourceCatalog '<resources><string-array name="key"><item>A</item><item>B</item></string-array></resources>'
        $localized = New-TestResourceCatalog '<resources><string-array name="key"><item>A</item></string-array></resources>'
        Assert-ResourceRejection { Test-MobileResourceCatalog $reference $localized 'array' } 'Array length mismatch'
    },
    {
        $reference = New-TestResourceCatalog '<resources><string name="key">A</string></resources>'
        $localized = New-TestResourceCatalog '<resources><string-array name="key"><item>A</item></string-array></resources>'
        Assert-ResourceRejection { Test-MobileResourceCatalog $reference $localized 'type' } 'Resource type mismatch'
    },
    {
        if ((Get-MobileLocaleFolder 'id') -cne 'values-b+id' -or (Get-MobileLocaleFolder 'in') -cne 'values-b+id' `
            -or (Get-MobileLocaleFolder 'tl') -cne 'values-b+tl' -or (Get-MobileLocaleFolder 'fil') -cne 'values-b+fil') {
            throw 'Incorrect BCP-47 resource directory aliases'
        }
    },
    {
        $gradle = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'game-app/build.gradle') -Raw
        if ($gradle -notmatch 'bundle\s*\{\s*language\s*\{\s*enableSplit\s*=\s*false') {
            throw 'Dynamic locale resources must not be split out of the base bundle'
        }
    },
    {
        $tutorial = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'game-app/src/main/java/net/munique/openmu/game/GestureTutorial.java') -Raw
        if ($tutorial -notmatch 'new AlertDialog\.Builder\(activity, android\.R\.style\.Theme_Material_Dialog_Alert\)' `
            -or $tutorial -notmatch '\.setMessage\(R\.string\.tutorial_body\)' `
            -or $tutorial -notmatch 'afterConfirm\.run\(\)') {
            throw 'Tutorial must retain its dark message dialog and confirmation callback'
        }
    }
)
foreach ($check in $checks) { & $check }
Write-Host "Mobile resource validation regressions: $($checks.Count)/$($checks.Count) passed."
