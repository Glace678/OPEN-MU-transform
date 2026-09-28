[CmdletBinding()]
param(
    [switch]$UpdateGenerated
)

$ErrorActionPreference = 'Stop'

function Get-RepositoryRoot {
    return (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
}

function Get-InterfaceIdentifiers([string]$EnumPath) {
    $source = [IO.File]::ReadAllText($EnumPath)
    $enumMatch = [regex]::Match(
        $source,
        'enum\s+INTERFACE_LIST\s*\{(?<body>[\s\S]*?)\};')

    if (-not $enumMatch.Success) {
        throw "INTERFACE_LIST was not found in $EnumPath"
    }

    $sentinels = @(
        'INTERFACE_BEGIN',
        'INTERFACE_3DRENDERING_CAMERA_BEGIN',
        'INTERFACE_3DRENDERING_CAMERA_END',
        'INTERFACE_END',
        'INTERFACE_COUNT'
    )

    return [regex]::Matches(
        $enumMatch.Groups['body'].Value,
        '(?m)^\s*(INTERFACE_[A-Z0-9_]+)') |
        ForEach-Object { $_.Groups[1].Value } |
        Where-Object { $_ -notin $sentinels } |
        Sort-Object -Unique
}

function Get-RegisteredInterfaceIdentifiers([string]$SourceRoot) {
    $identifiers = [Collections.Generic.HashSet[string]]::new(
        [StringComparer]::Ordinal)

    Get-ChildItem $SourceRoot -Recurse -Filter '*.cpp' | ForEach-Object {
        $source = [IO.File]::ReadAllText($_.FullName)
        [regex]::Matches(
            $source,
            'AddUIObj\s*\(\s*(?:SEASON3B::)?(INTERFACE_[A-Z0-9_]+)') |
            ForEach-Object { [void]$identifiers.Add($_.Groups[1].Value) }
    }

    return @($identifiers | Sort-Object)
}

function Get-RegisteredInterfaceSites([string]$SourceRoot) {
    $sites = [Collections.Generic.HashSet[string]]::new(
        [StringComparer]::Ordinal)

    Get-ChildItem $SourceRoot -Recurse -Filter '*.cpp' | ForEach-Object {
        $source = [IO.File]::ReadAllText($_.FullName)
        $relativePath = $_.FullName.Substring($SourceRoot.Length + 1).Replace('\', '/')
        [regex]::Matches(
            $source,
            'AddUIObj\s*\(\s*(?:SEASON3B::)?(?<id>INTERFACE_[A-Z0-9_]+|iAvailableCameraIndex)') |
            ForEach-Object {
                [void]$sites.Add("$relativePath|$($_.Groups['id'].Value)")
            }
    }

    return @($sites | Sort-Object)
}

function Get-PacketEntrypoints([string]$DotnetRoot) {
    $entrypoints = [Collections.Generic.HashSet[string]]::new(
        [StringComparer]::Ordinal)

    Get-ChildItem $DotnetRoot -Filter 'PacketFunctions*.h' | ForEach-Object {
        $source = [IO.File]::ReadAllText($_.FullName)
        [regex]::Matches(
            $source,
            '(?m)^\s*void\s+(Send[A-Za-z0-9_]+)\s*\(') |
            ForEach-Object { [void]$entrypoints.Add($_.Groups[1].Value) }
    }

    return @($entrypoints | Sort-Object)
}

function Get-PacketCategory([string]$Entrypoint) {
    $categories = [ordered]@{
        Protocol = 'Authenticate|Checksum|ClientNeedsPatch|ConnectionInfo|Hello|KeepAlive|Patch|Ping'
        Session = 'Login|LogOut|Server|ClientReady|RequestCharacterList|SelectCharacter|CreateCharacter|DeleteCharacter'
        Combat = 'Hit|Skill|Attack|Magic|Animation|TeleportTarget|FocusCharacter'
        Navigation = 'Walk|EnterGate|InstantMove|Warp|Pickup|TalkToNpc|CloseNpc'
        ShopTrade = 'Trade|PlayerShop|BuyItem|SellItem|CashShop'
        Inventory = 'Item|Inventory|Repair|Consume|Drop|Vault|Lahap|ChaosMachine|Crafting'
        Social = 'Friend|Chat|Whisper|Public|Letter|Party|Guild|Alliance|Duel'
        Character = 'Stat|Master|ResetCharacter|Pet|SaveKey'
        Automation = 'MuHelper'
        QuestEvent = 'Quest|BloodCastle|ChaosCastle|DevilSquare|Doppel|Illusion|Empire|Lucky|Event|Kanturu|Crywolf|Raklion|Gens|Castle|Catapult|Werewolf|Gatekeeper|Santa|Snowman|WhiteAngel|LeoHelper|Muto|MiniGame|NpcBuff|WeaponExplosion|MarketPlace'
    }

    foreach ($category in $categories.GetEnumerator()) {
        if ($Entrypoint -match $category.Value) {
            return $category.Key
        }
    }

    return 'Unclassified'
}

function Get-LegacyWindowIdentifiers([string]$LegacyRoot) {
    $uiManagerPath = Join-Path $LegacyRoot 'UIMng.cpp'
    $source = [IO.File]::ReadAllText($uiManagerPath)
    return [regex]::Matches(
        $source,
        'm_WinList\.AddHead\s*\(\s*&(?<name>m_[A-Za-z0-9_]+)') |
        ForEach-Object { "LEGACY_$($_.Groups['name'].Value)" } |
        Sort-Object -Unique
}

function Get-DynamicWindowTypeIdentifiers([string]$LegacyRoot) {
    $windowHeaderPath = Join-Path $LegacyRoot 'UIWindows.h'
    $source = [IO.File]::ReadAllText($windowHeaderPath)
    $enumMatch = [regex]::Match(
        $source,
        'enum\s+UIWINDOWSTYPE\s*\{(?<body>[\s\S]*?)\};')

    if (-not $enumMatch.Success) {
        throw "UIWINDOWSTYPE was not found in $windowHeaderPath"
    }

    return [regex]::Matches(
        $enumMatch.Groups['body'].Value,
        'UIWNDTYPE_[A-Z0-9_]+') |
        ForEach-Object { $_.Value } |
        Where-Object { $_ -ne 'UIWNDTYPE_EMPTY' } |
        ForEach-Object { "DYNAMIC_$_" } |
        Sort-Object -Unique
}

function Get-ActionSendSites([string]$SourceRoot) {
    $sites = [Collections.Generic.HashSet[string]]::new(
        [StringComparer]::Ordinal)
    $definitionPattern = '(?ms)^\s*(?:[A-Za-z_][A-Za-z0-9_:<>]*[\s*&]+)+(?<name>(?:[A-Za-z_][A-Za-z0-9_]*::)*Send[A-Za-z0-9_]+)\s*\([^;{}]*?\)\s*(?:const\s*)?\{'

    Get-ChildItem $SourceRoot -Recurse -Filter '*.cpp' | ForEach-Object {
        if ($_.Name -like 'PacketFunctions*.cpp') {
            return
        }

        $source = [IO.File]::ReadAllText($_.FullName)
        $relativePath = $_.FullName.Substring($SourceRoot.Length + 1).Replace('\', '/')
        [regex]::Matches($source, $definitionPattern) | ForEach-Object {
            [void]$sites.Add("$relativePath|$($_.Groups['name'].Value)")
        }
    }

    return @($sites | Sort-Object)
}

function Get-DependencyStatistics([string]$SourceRoot) {
    $mousePattern = 'MouseLButton|MouseRButton|MouseLButtonPush|MouseRButtonPush|MouseLButtonDBClick|VK_LBUTTON|VK_RBUTTON|SDL_BUTTON_LEFT|SDL_BUTTON_RIGHT'
    $textPattern = 'CUITextInputBox|CUIEditBox|InputBox|TextInput|EditBox'
    $mouseFiles = 0
    $mouseReferences = 0
    $textFiles = 0
    $textReferences = 0

    Get-ChildItem $SourceRoot -Recurse -Include '*.cpp', '*.h' | ForEach-Object {
        $source = [IO.File]::ReadAllText($_.FullName)
        $mouseMatches = [regex]::Matches($source, $mousePattern).Count
        $textMatches = [regex]::Matches($source, $textPattern).Count

        if ($mouseMatches -gt 0) {
            $mouseFiles++
            $mouseReferences += $mouseMatches
        }

        if ($textMatches -gt 0) {
            $textFiles++
            $textReferences += $textMatches
        }
    }

    return [pscustomobject]@{
        MouseFiles = $mouseFiles
        MouseReferences = $mouseReferences
        TextFiles = $textFiles
        TextReferences = $textReferences
    }
}

function Compare-ExactSet(
    [string]$Label,
    [string[]]$Expected,
    [string[]]$Actual,
    [Collections.Generic.List[string]]$Errors) {
    $missing = @($Expected | Where-Object { $_ -notin $Actual })
    $unexpected = @($Actual | Where-Object { $_ -notin $Expected })

    if ($missing.Count -gt 0) {
        $Errors.Add("$Label missing from source: $($missing -join ', ')")
    }

    if ($unexpected.Count -gt 0) {
        $Errors.Add("$Label not registered in coverage data: $($unexpected -join ', ')")
    }
}

function Convert-ToMarkdown(
    [object[]]$Rows,
    [string[]]$Interfaces,
    [string[]]$Registrations,
    [string[]]$RegistrationSites,
    [string[]]$LegacyWindows,
    [string[]]$DynamicWindowTypes,
    [string[]]$ActionSendSites,
    [string[]]$Packets,
    [pscustomobject]$Statistics) {
    $lines = [Collections.Generic.List[string]]::new()
    $lines.Add('# Controller coverage audit')
    $lines.Add('')
    $lines.Add('This file is generated from `docs/controller-coverage.csv`. Run `tools/ControllerCoverage/Test-ControllerCoverage.ps1 -UpdateGenerated` after an intentional registry update.')
    $lines.Add('')
    $lines.Add('Static coverage means that a controller route is documented and reachable through the common semantic-input or virtual-pointer layer. It does not replace runtime replay or real-controller validation.')
    $lines.Add('')
    $lines.Add('## Inventory')
    $lines.Add('')
    $lines.Add('| Measure | Count |')
    $lines.Add('| --- | ---: |')
    $lines.Add("| `INTERFACE_LIST` player interfaces | $($Interfaces.Count) |")
    $lines.Add("| `AddUIObj` registered interface identifiers | $($Registrations.Count) |")
    $lines.Add("| `AddUIObj` source registration sites | $($RegistrationSites.Count) |")
    $lines.Add("| `CUIMng` fixed legacy windows | $($LegacyWindows.Count) |")
    $lines.Add("| `UIWINDOWSTYPE` dynamic window types | $($DynamicWindowTypes.Count) |")
    $lines.Add("| Legacy scenes/workflows | $(@($Rows | Where-Object { $_.Kind -in @('LegacyScene', 'LegacyWorkflow', 'Gameplay') }).Count) |")
    $lines.Add("| Non-`PacketFunctions` `Send*` helper sites | $($ActionSendSites.Count) |")
    $lines.Add("| Client `Send*` entrypoints | $($Packets.Count) |")
    $lines.Add("| Files with mouse-button dependencies | $($Statistics.MouseFiles) |")
    $lines.Add("| Mouse-button dependency references | $($Statistics.MouseReferences) |")
    $lines.Add("| Files with text-input dependencies | $($Statistics.TextFiles) |")
    $lines.Add("| Text-input dependency references | $($Statistics.TextReferences) |")
    $lines.Add('')
    $lines.Add('## Network action families')
    $lines.Add('')
    $lines.Add('| Family | `Send*` entrypoints |')
    $lines.Add('| --- | ---: |')
    $Packets | Group-Object { Get-PacketCategory $_ } | Sort-Object Name | ForEach-Object {
        $lines.Add("| $($_.Name) | $($_.Count) |")
    }
    $lines.Add('')
    $lines.Add('## Status')
    $lines.Add('')
    $lines.Add('| Implementation status | Entries |')
    $lines.Add('| --- | ---: |')
    $Rows | Group-Object Implementation | Sort-Object Name | ForEach-Object {
        $lines.Add("| $($_.Name) | $($_.Count) |")
    }
    $lines.Add('')
    $lines.Add('| Validation status | Entries |')
    $lines.Add('| --- | ---: |')
    $Rows | Group-Object Validation | Sort-Object Name | ForEach-Object {
        $lines.Add("| $($_.Name) | $($_.Count) |")
    }
    $lines.Add('')
    $lines.Add('## Coverage matrix')
    $lines.Add('')
    $lines.Add('| ID | Kind | Category | Player workflow | Controller route | Text entry | Haptic confirmation | Implementation | Validation |')
    $lines.Add('| --- | --- | --- | --- | --- | --- | --- | --- | --- |')
    foreach ($row in $Rows) {
        $values = @(
            $row.Id,
            $row.Kind,
            $row.Category,
            $row.PlayerWorkflow,
            $row.ControllerPath,
            $row.TextEntry,
            $row.HapticConfirmation,
            $row.Implementation,
            $row.Validation
        ) | ForEach-Object { ([string]$_).Replace('|', '\|').Replace("`r", ' ').Replace("`n", ' ') }
        $lines.Add("| $($values -join ' | ') |")
    }
    $lines.Add('')
    $lines.Add('## Acceptance gate')
    $lines.Add('')
    $lines.Add('- The audit fails when a concrete `INTERFACE_LIST` value is absent from the CSV.')
    $lines.Add('- The audit fails when an `AddUIObj` registration is absent from the CSV.')
    $lines.Add('- The audit fails when an `AddUIObj` source registration site changes without review, even if it reuses an existing identifier.')
    $lines.Add('- The audit fails when any generated or custom `PacketFunctions*.h` adds or removes a `Send*` entrypoint without updating the exact baseline.')
    $lines.Add('- The audit also baselines non-`PacketFunctions` `Send*` helper definitions, including old combat/item wrappers and UI dispatch helpers, so bypass paths require review.')
    $lines.Add('- `PointerFallback` is intentionally not equivalent to native focus navigation. Runtime validation remains required for drag/hold/release, double-click, text composition and server-confirmed haptics.')
    $lines.Add('')
    return ($lines -join "`n") + "`n"
}

$repositoryRoot = Get-RepositoryRoot
$sourceRoot = Join-Path $repositoryRoot 'src\source'
$coveragePath = Join-Path $repositoryRoot 'docs\controller-coverage.csv'
$generatedPath = Join-Path $repositoryRoot 'docs\controller-coverage.md'
$packetBaselinePath = Join-Path $PSScriptRoot 'packet-entrypoints.txt'
$registrationBaselinePath = Join-Path $PSScriptRoot 'newui-registration-sites.txt'
$actionSendBaselinePath = Join-Path $PSScriptRoot 'action-send-sites.txt'
$enumPath = Join-Path $sourceRoot 'Core\Globals\_enum.h'
$dotnetRoot = Join-Path $sourceRoot 'Dotnet'
$legacyRoot = Join-Path $sourceRoot 'UI\Legacy'

$requiredColumns = @(
    'Id', 'Kind', 'Category', 'PlayerWorkflow', 'Operations',
    'ControllerPath', 'TextEntry', 'HapticConfirmation',
    'Implementation', 'Validation', 'Evidence'
)
$requiredWorkflows = @(
    'SCENE_LOGIN',
    'SCENE_SERVER_LIST',
    'SCENE_CHARACTER_SELECT',
    'SCENE_CHARACTER_CREATE_DELETE',
    'WORLD_MOVEMENT_PICKUP',
    'WORLD_COMBAT_SKILLS',
    'WORLD_PET_POTIONS_HELPER',
    'WORKFLOW_GUILD_STORAGE',
    'WORKFLOW_CHAT_MAIL',
    'WORKFLOW_RECONNECT_ERRORS',
    'EDITOR_IMGUI'
)

$errors = [Collections.Generic.List[string]]::new()
$rows = @(Import-Csv $coveragePath)
if ($rows.Count -eq 0) {
    throw "Coverage registry is empty: $coveragePath"
}

$actualColumns = @($rows[0].PSObject.Properties.Name)
$missingColumns = @($requiredColumns | Where-Object { $_ -notin $actualColumns })
if ($missingColumns.Count -gt 0) {
    $errors.Add("Coverage CSV is missing columns: $($missingColumns -join ', ')")
}

$duplicateIds = @($rows | Group-Object Id | Where-Object Count -gt 1)
if ($duplicateIds.Count -gt 0) {
    $errors.Add("Coverage CSV contains duplicate IDs: $($duplicateIds.Name -join ', ')")
}

$emptyFields = @($rows | Where-Object {
    foreach ($column in $requiredColumns) {
        if ([string]::IsNullOrWhiteSpace([string]$_.$column)) {
            return $true
        }
    }
    return $false
})
if ($emptyFields.Count -gt 0) {
    $errors.Add("Coverage CSV contains empty required fields: $($emptyFields.Id -join ', ')")
}

$interfaces = @(Get-InterfaceIdentifiers $enumPath)
$registrations = @(Get-RegisteredInterfaceIdentifiers $sourceRoot)
$registrationSites = @(Get-RegisteredInterfaceSites $sourceRoot)
$matrixInterfaces = @($rows | Where-Object Kind -eq 'NewUI' | ForEach-Object Id | Sort-Object -Unique)
Compare-ExactSet 'INTERFACE_LIST identifiers' $matrixInterfaces $interfaces $errors

$unknownRegistrations = @($registrations | Where-Object { $_ -notin $matrixInterfaces })
if ($unknownRegistrations.Count -gt 0) {
    $errors.Add("AddUIObj registrations absent from coverage CSV: $($unknownRegistrations -join ', ')")
}


$registrationBaseline = @(Get-Content $registrationBaselinePath | Where-Object {
    -not [string]::IsNullOrWhiteSpace($_) -and -not $_.StartsWith('#')
} | Sort-Object -Unique)
Compare-ExactSet 'AddUIObj source registration sites' $registrationBaseline $registrationSites $errors

$missingWorkflows = @($requiredWorkflows | Where-Object { $_ -notin $rows.Id })
if ($missingWorkflows.Count -gt 0) {
    $errors.Add("Required legacy workflows absent from coverage CSV: $($missingWorkflows -join ', ')")
}

$legacyWindows = @(Get-LegacyWindowIdentifiers $legacyRoot)
$matrixLegacyWindows = @($rows | Where-Object Kind -eq 'LegacyWindow' | ForEach-Object Id | Sort-Object -Unique)
Compare-ExactSet 'CUIMng fixed legacy windows' $matrixLegacyWindows $legacyWindows $errors

$dynamicWindowTypes = @(Get-DynamicWindowTypeIdentifiers $legacyRoot)
$matrixDynamicWindowTypes = @($rows | Where-Object Kind -eq 'DynamicWindow' | ForEach-Object Id | Sort-Object -Unique)
Compare-ExactSet 'UIWINDOWSTYPE dynamic windows' $matrixDynamicWindowTypes $dynamicWindowTypes $errors

$actionSendSites = @(Get-ActionSendSites $sourceRoot)
$actionSendBaseline = @(Get-Content $actionSendBaselinePath | Where-Object {
    -not [string]::IsNullOrWhiteSpace($_) -and -not $_.StartsWith('#')
} | Sort-Object -Unique)
Compare-ExactSet 'Non-PacketFunctions Send helper sites' $actionSendBaseline $actionSendSites $errors

$packets = @(Get-PacketEntrypoints $dotnetRoot)
$packetBaseline = @(Get-Content $packetBaselinePath | Where-Object {
    -not [string]::IsNullOrWhiteSpace($_) -and -not $_.StartsWith('#')
} | Sort-Object -Unique)
Compare-ExactSet 'Packet Send entrypoints' $packetBaseline $packets $errors
$unclassifiedPackets = @($packets | Where-Object { (Get-PacketCategory $_) -eq 'Unclassified' })
if ($unclassifiedPackets.Count -gt 0) {
    $errors.Add("Packet Send entrypoints require a reviewed action family: $($unclassifiedPackets -join ', ')")
}

$statistics = Get-DependencyStatistics $sourceRoot
$markdown = Convert-ToMarkdown $rows $interfaces $registrations $registrationSites $legacyWindows $dynamicWindowTypes $actionSendSites $packets $statistics
if ($UpdateGenerated) {
    [IO.File]::WriteAllText($generatedPath, $markdown, [Text.UTF8Encoding]::new($false))
}
elseif (-not (Test-Path $generatedPath)) {
    $errors.Add("Generated report does not exist: $generatedPath")
}
else {
    $existing = [IO.File]::ReadAllText($generatedPath).Replace("`r`n", "`n")
    if ($existing -ne $markdown) {
        $errors.Add('Generated report is stale. Run this script with -UpdateGenerated.')
    }
}

if ($errors.Count -gt 0) {
    $errors | ForEach-Object { Write-Error $_ }
    exit 1
}

Write-Host "Controller coverage audit passed: $($rows.Count) matrix entries, $($interfaces.Count) interfaces, $($registrationSites.Count) NewUI registration sites, $($legacyWindows.Count) fixed legacy windows, $($dynamicWindowTypes.Count) dynamic window types, $($packets.Count) packet entrypoints, $($actionSendSites.Count) legacy/action Send helpers."
Write-Host "Mouse dependencies: $($statistics.MouseReferences) references in $($statistics.MouseFiles) files; text dependencies: $($statistics.TextReferences) references in $($statistics.TextFiles) files."
