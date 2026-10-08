<#
.SYNOPSIS
Builds the local OpenMU source and starts its persistent all-in-one server.

.DESCRIPTION
DryRun reads configuration and prints the plan without Docker or filesystem writes.
The default deployment never pulls the upstream OpenMU image or uploads source.
#>
[CmdletBinding()]
param(
    [ValidatePattern('^[A-Za-z0-9_.-]{3,32}$')]
    [string]$AdminUser = 'admin',
    [string]$AdminPassword,
    [string]$PublicAddress,
    [string]$PortalDomain,
    [ValidateRange(1, 65535)]
    [int]$WebPort = 80,
    [string]$ConfigDirectory,
    [switch]$Build,
    [switch]$PublishedImage,
    [switch]$NoPull,
    [switch]$Down,
    [switch]$Logs,
    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'

function New-Secret {
    $buffer = New-Object byte[] 24
    $generator = [Security.Cryptography.RandomNumberGenerator]::Create()
    try {
        $generator.GetBytes($buffer)
    } finally {
        $generator.Dispose()
    }
    return [Convert]::ToBase64String($buffer).TrimEnd('=').Replace('+', '-').Replace('/', '_')
}

# Generates a 32-char admin password which satisfies the panel password policy:
# at least one uppercase, lowercase, digit and non-alphanumeric character.
function New-AdminSecret {
    $generator = [Security.Cryptography.RandomNumberGenerator]::Create()
    try {
        $rest = New-Secret
        if ($rest.Length -gt 28) { $rest = $rest.Substring(0, 28) }
        $b = New-Object byte[] 3
        $generator.GetBytes($b)
        $upper = [char](65 + ($b[0] % 26))
        $lower = [char](97 + ($b[1] % 26))
        $digit = [char](48 + ($b[2] % 10))
        $chars = ($rest + $upper + $lower + $digit + '-').ToCharArray()
        # Fisher-Yates shuffle.
        for ($i = $chars.Length - 1; $i -gt 0; $i--) {
            $j = $generator.GetInt32(0, $i + 1)
            ($chars[$i], $chars[$j]) = ($chars[$j], $chars[$i])
        }

        return -join $chars
    } finally {
        $generator.Dispose()
    }
}

function Invoke-Compose([string[]]$Arguments) {
    # Compose otherwise lets inherited shell variables override the saved .env file.
    $savedEnvironment = @{}
    $composeVariables = @('DB_ADMIN_USER', 'DB_ADMIN_PW', 'OPENMU_ADMIN_USER', 'OPENMU_ADMIN_PASSWORD',
        'OPENMU_ADMIN_TOTP_SECRET', 'RESOLVE_IP', 'OPENMU_GAME_BIND', 'OPENMU_WEB_PORT', 'POSTGRES_IMAGE', 'OPENMU_PORTAL_DOMAIN')
    try {
        foreach ($name in $composeVariables) {
            $savedEnvironment[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
            [Environment]::SetEnvironmentVariable($name, $null, 'Process')
        }
        & docker compose @Arguments
        if ($LASTEXITCODE -ne 0) {
            throw "Docker Compose failed with exit code $LASTEXITCODE."
        }
    } finally {
        foreach ($name in $composeVariables) {
            [Environment]::SetEnvironmentVariable($name, $savedEnvironment[$name], 'Process')
        }
    }
}

function Assert-AdvertisedAddress([string]$Address) {
    $parsedAddress = $null
    if ($Address -notmatch '^\d{1,3}(\.\d{1,3}){3}$' -or
        -not [Net.IPAddress]::TryParse($Address, [ref]$parsedAddress)) {
        throw 'PublicAddress must be the IPv4 address reachable by every game client (no URL or port).'
    }
    $firstOctet = $parsedAddress.GetAddressBytes()[0]
    if ($firstOctet -eq 0 -or $firstOctet -eq 127 -or $firstOctet -ge 224) {
        throw 'PublicAddress must not be unspecified, loopback, multicast, or broadcast.'
    }
}

function Assert-PortalDomain([string]$Domain) {
    if ($Domain.Length -gt 253 -or
        $Domain -notmatch '^(?=.{1,253}$)(?:[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?\.)+[a-z](?:[a-z0-9-]{0,61}[a-z0-9])?$' -or
        $Domain -match '\.(localhost|local|internal)$') {
        throw 'PortalDomain must be a public DNS hostname, not an IP address, URL, port, or local hostname.'
    }
}

if ($Build -and $PublishedImage) {
    throw 'Build and PublishedImage cannot be combined. The default already builds local source.'
}
if ($PSBoundParameters.ContainsKey('PublicAddress')) {
    Assert-AdvertisedAddress $PublicAddress
}
if ($PSBoundParameters.ContainsKey('PortalDomain')) { Assert-PortalDomain $PortalDomain }
if ($AdminPassword -and $AdminPassword -notmatch '^[A-Za-z0-9_.-]{12,128}$') {
    throw 'AdminPassword must be 12-128 URL-safe letters, digits, dots, hyphens, or underscores.'
}

$composeDirectory = Join-Path $PSScriptRoot 'all-in-one'
$sourceDirectory = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\src'))
if (-not (Test-Path -LiteralPath $composeDirectory -PathType Container)) {
    throw "Compose directory not found: $composeDirectory"
}
if (-not $ConfigDirectory) {
    $ConfigDirectory = $composeDirectory
}
$ConfigDirectory = [IO.Path]::GetFullPath($ConfigDirectory)
$envPath = Join-Path $ConfigDirectory '.env'
$envExists = Test-Path -LiteralPath $envPath -PathType Leaf
$values = [ordered]@{}
$envLines = @()
if ($envExists) {
    $envLines = @(Get-Content -LiteralPath $envPath)
    foreach ($line in $envLines) {
        if ($line -match '^([A-Za-z_][A-Za-z0-9_]*)=(.*)$') {
            $key = $Matches[1]
            $value = $Matches[2].Trim()
            if ($values.Contains($key)) {
                throw "Duplicate key '$key' in $envPath. Remove the duplicate before deploying."
            }
            $values[$key] = $value
        } elseif ($line.Trim() -and $line -notmatch '^\s*#') {
            throw "Unsupported assignment in $envPath; use KEY=value without quotes or spaces."
        }
    }
}

if (-not $Down) {
    if ($envExists) {
        foreach ($key in @('OPENMU_ADMIN_USER', 'OPENMU_ADMIN_PASSWORD', 'DB_ADMIN_USER', 'DB_ADMIN_PW')) {
            if (-not $values[$key]) {
                throw "Missing '$key' in $envPath. Existing credentials are never regenerated."
            }
        }
        if ($values['DB_NAME'] -and $values['DB_NAME'] -ne 'openmu') {
            throw 'OpenMU ConnectionSettings.xml uses the database name openmu. DB_NAME cannot select another database.'
        }
        if ($values['DB_ADMIN_USER'] -notmatch '^[A-Za-z_][A-Za-z0-9_]{0,62}$' -or
            $values['OPENMU_ADMIN_USER'] -notmatch '^[A-Za-z0-9_.-]{3,32}$') {
            throw 'Existing database/admin user names contain unsupported characters.'
        }
        foreach ($key in @('OPENMU_ADMIN_PASSWORD', 'DB_ADMIN_PW')) {
            if ($values[$key] -notmatch '^[A-Za-z0-9_.-]{12,128}$') {
                throw "'$key' must contain 12-128 URL-safe characters. Credentials are not changed automatically."
            }
        }
        if (($PSBoundParameters.ContainsKey('AdminUser') -and $AdminUser -ne $values['OPENMU_ADMIN_USER']) -or
            ($AdminPassword -and $AdminPassword -ne $values['OPENMU_ADMIN_PASSWORD'])) {
            throw 'Existing credentials are kept. Change admin credentials through the admin panel, not the deployment script.'
        }
    } else {
        $values['OPENMU_ADMIN_USER'] = $AdminUser
        $values['OPENMU_ADMIN_PASSWORD'] = '<generated-at-start>'
        $values['DB_ADMIN_USER'] = 'postgres'
        $values['DB_ADMIN_PW'] = '<generated-at-start>'
        $values['DB_NAME'] = 'openmu'
    }

    if (-not $values['RESOLVE_IP']) { $values['RESOLVE_IP'] = '127.0.0.1' }
    if (-not $values['OPENMU_GAME_BIND']) { $values['OPENMU_GAME_BIND'] = '127.0.0.1' }
    if (-not $values['OPENMU_WEB_PORT']) { $values['OPENMU_WEB_PORT'] = '80' }
    if ($PublicAddress) {
        $values['RESOLVE_IP'] = $PublicAddress
        $values['OPENMU_GAME_BIND'] = '0.0.0.0'
    }
    if ($PSBoundParameters.ContainsKey('WebPort')) { $values['OPENMU_WEB_PORT'] = "$WebPort" }
    if ($PortalDomain) { $values['OPENMU_PORTAL_DOMAIN'] = $PortalDomain.ToLowerInvariant() }
    if ($values['OPENMU_GAME_BIND'] -notin @('127.0.0.1', '0.0.0.0')) {
        throw 'OPENMU_GAME_BIND must be 127.0.0.1 or 0.0.0.0.'
    }
    $configuredPort = 0
    if (-not [int]::TryParse($values['OPENMU_WEB_PORT'], [ref]$configuredPort) -or
        $configuredPort -lt 1 -or $configuredPort -gt 65535) {
        throw 'OPENMU_WEB_PORT must be between 1 and 65535.'
    }
    if ($values['RESOLVE_IP'] -ne '127.0.0.1') {
        Assert-AdvertisedAddress $values['RESOLVE_IP']
    } elseif ($values['OPENMU_GAME_BIND'] -eq '0.0.0.0') {
        throw 'Public game ports require an explicit reachable RESOLVE_IP address.'
    }
}

if ($values['OPENMU_PORTAL_DOMAIN']) {
    Assert-PortalDomain $values['OPENMU_PORTAL_DOMAIN']
    if (-not $Down -and ($PublishedImage -or $values['RESOLVE_IP'] -eq '127.0.0.1')) {
        throw 'The public player portal requires local-source mode and an explicit PublicAddress reachable by game clients.'
    }
}

$composePrefix = @('--project-directory', $composeDirectory, '--project-name', 'all-in-one')
if ($envExists -or -not $Down) { $composePrefix += @('--env-file', $envPath) }
$composePrefix += @('-f', (Join-Path $composeDirectory 'docker-compose.yml'))
if (-not $PublishedImage) {
    $composePrefix += @('-f', (Join-Path $composeDirectory 'docker-compose.local.yml'))
}
if ($values['OPENMU_PORTAL_DOMAIN']) {
    $composePrefix += @('-f', (Join-Path $composeDirectory 'docker-compose.portal.yml'))
}
$commands = @()
if ($Down) {
    if ($envExists) { $commands += ,($composePrefix + @('down')) }
} else {
    $commands += ,($composePrefix + @('config', '--quiet'))
    if ($PublishedImage) {
        if (-not $NoPull) { $commands += ,($composePrefix + @('pull', 'openmu-startup')) }
    } else {
        $commands += ,($composePrefix + @('build', 'openmu-startup'))
    }
    $commands += ,($composePrefix + @('up', '-d', '--no-build'))
    if ($Logs) { $commands += ,($composePrefix + @('logs', '-f', 'openmu-startup')) }
}

if ($DryRun) {
    [pscustomobject]@{
        ImageMode = $(if ($PublishedImage) { 'published-upstream' } else { 'local-source' })
        BuildContext = $sourceDirectory
        EnvFile = $envPath
        WouldCreateCredentials = (-not $envExists -and -not $Down)
        AdvertisedAddress = $values['RESOLVE_IP']
        GameBind = $values['OPENMU_GAME_BIND']
        AdminBind = '127.0.0.1'
        WebPort = $values['OPENMU_WEB_PORT']
        DatabaseName = 'openmu'
        PlayerPortal = $(if ($values['OPENMU_PORTAL_DOMAIN']) { "https://$($values['OPENMU_PORTAL_DOMAIN'])" } else { $null })
        Commands = $commands
    }
    return
}

if ($Down -and -not $envExists) {
    Write-Host 'No deployment credentials file exists. Nothing was stopped or deleted.'
    return
}

if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    throw 'Docker is required. Install Docker Desktop or Docker Engine with the Compose plugin first.'
}
Invoke-Compose @('version')

if (-not $Down) {
    if (-not $envExists) {
        $values['OPENMU_ADMIN_PASSWORD'] = $(if ($AdminPassword) { $AdminPassword } else { New-AdminSecret })
        $values['DB_ADMIN_PW'] = New-Secret
    }
    $outputLines = [Collections.Generic.List[string]]::new()
    $writtenKeys = @{}
    foreach ($line in $envLines) {
        if ($line -match '^\s*([A-Za-z_][A-Za-z0-9_]*)\s*=') {
            $key = $Matches[1]
            $outputLines.Add("$key=$($values[$key])")
            $writtenKeys[$key] = $true
        } else {
            $outputLines.Add($line)
        }
    }
    foreach ($key in $values.Keys) {
        if (-not $writtenKeys[$key]) { $outputLines.Add("$key=$($values[$key])") }
    }
    $updatedText = ($outputLines -join [Environment]::NewLine) + [Environment]::NewLine
    if (-not $envExists -or [IO.File]::ReadAllText($envPath) -ne $updatedText) {
        [IO.Directory]::CreateDirectory($ConfigDirectory) | Out-Null
        [IO.File]::WriteAllText($envPath, $updatedText, [Text.UTF8Encoding]::new($false))
    }
}

foreach ($command in $commands) { Invoke-Compose $command }
if ($Down) {
    Write-Host 'OpenMU containers stopped. Database volumes were preserved.'
    return
}
Write-Host "OpenMU startup requested. Admin panel: http://127.0.0.1:$($values['OPENMU_WEB_PORT'])/"
Write-Host "MuMain connection: $($values['RESOLVE_IP']):44406 (TCP, fresh Season 6 open-source configuration)"
Write-Host 'The legacy GMO client uses 44405. Existing database endpoint settings are kept.'
Write-Host "Admin user: $($values['OPENMU_ADMIN_USER']). Credentials kept locally in $envPath"
Write-Host 'The admin panel is loopback-only. Use an SSH tunnel for remote administration.'
if ($values['OPENMU_PORTAL_DOMAIN']) { Write-Host "Player accounts: https://$($values['OPENMU_PORTAL_DOMAIN'])/register (443 only; admin stays private)." }
if ($PublishedImage) { Write-Warning 'The published upstream image does not include local modifications.' }
