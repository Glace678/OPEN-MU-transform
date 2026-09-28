$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'GamePayloadPackaging.ps1')

$testRoot = Join-Path ([IO.Path]::GetTempPath()) ("OpenMU-GamePayloadTest-{0}" -f [Guid]::NewGuid().ToString('N'))
$source = Join-Path $testRoot 'source'
$destination = Join-Path $testRoot 'package\App\Game'
$junction = Join-Path $testRoot 'publish-link'
$junctionTarget = Join-Path $testRoot 'publish-target'
$ordinaryPublish = Join-Path $testRoot 'ordinary-publish'
$manifestPackage = Join-Path $testRoot 'manifest-package'

try {
    New-Item -ItemType Directory -Path $source -Force | Out-Null
    Set-Content -LiteralPath (Join-Path $source 'Main.exe') -Value 'test executable' -Encoding utf8NoBOM
    Set-Content -LiteralPath (Join-Path $source 'MuClient.lib') -Value 'linker artifact' -Encoding utf8NoBOM
    Set-Content -LiteralPath (Join-Path $source 'config.ini') -Encoding utf8NoBOM -Value @'
[LOGIN]
RememberMe=1
EncryptedUsername=remembered-user-secret
EncryptedPassword=remembered-password-secret
[UI]
Locale=en
'@
    Set-Content -LiteralPath (Join-Path $source 'config.ini.template') -Encoding utf8NoBOM -Value @'
[LOGIN]
RememberMe=0
EncryptedUsername=
EncryptedPassword=
[UI]
Locale=zh-CN
'@

    Copy-GamePayload -SourceDirectory $source -DestinationDirectory $destination

    $packagedConfig = Get-Content -LiteralPath (Join-Path $destination 'config.ini') -Raw
    $templateConfig = Get-Content -LiteralPath (Join-Path $source 'config.ini.template') -Raw
    if ($packagedConfig -cne $templateConfig) {
        throw 'Packaged config.ini is not an exact copy of config.ini.template.'
    }
    if ($packagedConfig.Contains('remembered-user-secret') -or $packagedConfig.Contains('remembered-password-secret')) {
        throw 'Packaged config.ini leaked remembered credentials from the build directory.'
    }
    Assert-GameConfigTemplate -TemplatePath (Join-Path $destination 'config.ini')
    if (Test-Path -LiteralPath (Join-Path $destination 'MuClient.lib')) {
        throw 'Packaged game payload contains a linker library.'
    }

    New-Item -ItemType Directory -Path (Join-Path $manifestPackage 'App\Game') -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $destination 'config.ini') -Destination (Join-Path $manifestPackage 'App\Game\config.ini')
    Copy-Item -LiteralPath (Join-Path $source 'config.ini.template') -Destination (Join-Path $manifestPackage 'App\Game\config.ini.template')
    & (Join-Path $PSScriptRoot 'New-PackageManifest.ps1') `
        -PackageRoot $manifestPackage `
        -Version '0.9.10-local.1' `
        -PostgreSqlVersion '17.11-1' `
        -PostgreSqlArchiveSha256 ('0' * 64) `
        -PostgreSqlSourceUrl 'https://get.enterprisedb.com/postgresql/test.zip' | Out-Null
    $manifestFiles = @((Get-Content -LiteralPath (Join-Path $manifestPackage 'manifest.json') -Raw | ConvertFrom-Json).files.path)
    if ($manifestFiles -contains 'App/Game/config.ini') {
        throw 'The mutable packaged config.ini was added to the integrity manifest.'
    }
    if ($manifestFiles -notcontains 'App/Game/config.ini.template') {
        throw 'The verified config.ini.template is missing from the integrity manifest.'
    }

    Set-Content -LiteralPath (Join-Path $source 'config.ini.template') -Encoding utf8NoBOM -Value @'
[LOGIN]
RememberMe=1
EncryptedUsername=
EncryptedPassword=
[UI]
Locale=zh-CN
'@
    $unsafeTemplateRejected = $false
    try {
        Assert-GameConfigTemplate -TemplatePath (Join-Path $source 'config.ini.template')
    } catch {
        $unsafeTemplateRejected = $true
    }
    if (-not $unsafeTemplateRejected) {
        throw 'An unsafe config.ini.template was accepted.'
    }

    Remove-Item -LiteralPath (Join-Path $source 'config.ini.template') -Force
    $missingTemplateRejected = $false
    try {
        Copy-GamePayload -SourceDirectory $source -DestinationDirectory $destination
    } catch {
        $missingTemplateRejected = $true
    }
    if (-not $missingTemplateRejected) {
        throw 'A game payload without config.ini.template was accepted.'
    }

    New-Item -ItemType Directory -Path $junctionTarget -Force | Out-Null
    Set-Content -LiteralPath (Join-Path $junctionTarget 'keep.txt') -Value 'must survive' -Encoding utf8NoBOM
    New-Item -ItemType Junction -Path $junction -Target $junctionTarget | Out-Null
    $junctionRejected = $false
    try {
        Remove-PackagingDirectorySafely -Path $junction
    } catch {
        $junctionRejected = $true
    }
    if (-not $junctionRejected) {
        throw 'A reparse-point packaging directory was accepted for recursive removal.'
    }
    if (-not (Test-Path -LiteralPath (Join-Path $junctionTarget 'keep.txt') -PathType Leaf)) {
        throw 'The reparse-point target was modified during the safety check.'
    }

    New-Item -ItemType Directory -Path $ordinaryPublish -Force | Out-Null
    Set-Content -LiteralPath (Join-Path $ordinaryPublish 'remove.txt') -Value 'temporary' -Encoding utf8NoBOM
    Remove-PackagingDirectorySafely -Path $ordinaryPublish
    if (Test-Path -LiteralPath $ordinaryPublish) {
        throw 'An ordinary packaging directory was not removed.'
    }

    Write-Output 'Game payload packaging test passed.'
} finally {
    if (Test-Path -LiteralPath $junction) {
        Remove-Item -LiteralPath $junction -Force
    }
    if (Test-Path -LiteralPath $testRoot) {
        Remove-Item -LiteralPath $testRoot -Recurse -Force
    }
}
