param(
    [ValidatePattern('^[0-9A-Fa-f]{64}$')]
    [string]$ExpectedSha256,

    [string]$SourceArchive,

    [Parameter(Mandatory = $true)]
    [string]$Destination
)

$ErrorActionPreference = 'Stop'

function Remove-DirectoryWithRetries([string]$Path) {
    for ($attempt = 1; $attempt -le 5; $attempt++) {
        if (-not (Test-Path -LiteralPath $Path)) {
            return $true
        }

        try {
            Remove-Item -LiteralPath $Path -Recurse -Force -ErrorAction Stop
            return $true
        } catch {
            if ($attempt -eq 5) {
                Write-Warning "Unable to remove '$Path' after $attempt attempts: $($_.Exception.Message)"
                return $false
            }

            Start-Sleep -Milliseconds (200 * $attempt)
        }
    }
}

$descriptorPath = Join-Path $PSScriptRoot 'postgresql-runtime.json'
$descriptor = Get-Content -LiteralPath $descriptorPath -Raw | ConvertFrom-Json
$resolvedExpectedSha256 = if ([string]::IsNullOrWhiteSpace($ExpectedSha256)) {
    [string]$descriptor.sha256
} else {
    $ExpectedSha256
}
if ($resolvedExpectedSha256 -notmatch '^[0-9A-Fa-f]{64}$') {
    throw 'The pinned PostgreSQL SHA-256 is missing or malformed.'
}
$destinationPath = [IO.Path]::GetFullPath($Destination)
$destinationParent = Split-Path -Parent $destinationPath
if ([IO.Path]::GetFileName($destinationPath) -ne 'PostgreSQL' -or [IO.Path]::GetFileName($destinationParent) -ne 'Runtime') {
    throw "Destination must end with Runtime\PostgreSQL; refusing to replace '$destinationPath'."
}

$runtimeUri = [Uri]$descriptor.url
if ($runtimeUri.Scheme -ne 'https' -or $runtimeUri.Host -ne 'get.enterprisedb.com' -or [IO.Path]::GetFileName($runtimeUri.AbsolutePath) -ne $descriptor.archiveName) {
    throw 'postgresql-runtime.json must point to the pinned HTTPS archive on get.enterprisedb.com.'
}

if (Test-Path -LiteralPath $destinationPath) {
    $destinationItem = Get-Item -LiteralPath $destinationPath -Force
    if (($destinationItem.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
        throw "Refusing to replace reparse-point PostgreSQL destination: $destinationPath"
    }
}

$temporaryRoot = Join-Path ([IO.Path]::GetTempPath()) ("openmu-postgresql-" + [Guid]::NewGuid().ToString('N'))
$archivePath = Join-Path $temporaryRoot $descriptor.archiveName
$extractPath = Join-Path $temporaryRoot 'extract'
$previousDestination = $destinationPath + '.previous-' + [Guid]::NewGuid().ToString('N')
$sourceArchivePath = if ([string]::IsNullOrWhiteSpace($SourceArchive)) {
    $null
} else {
    [IO.Path]::GetFullPath($SourceArchive)
}

if ($sourceArchivePath -and -not (Test-Path -LiteralPath $sourceArchivePath -PathType Leaf)) {
    throw "PostgreSQL source archive does not exist: $sourceArchivePath"
}

try {
    New-Item -ItemType Directory -Path $temporaryRoot,$extractPath -Force | Out-Null
    if ($sourceArchivePath) {
        Copy-Item -LiteralPath $sourceArchivePath -Destination $archivePath
    } else {
        Invoke-WebRequest -Uri $descriptor.url -OutFile $archivePath -UseBasicParsing
    }
    $actualHash = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash
    if (-not $actualHash.Equals($resolvedExpectedSha256, [StringComparison]::OrdinalIgnoreCase)) {
        throw "PostgreSQL archive SHA-256 mismatch. Expected $resolvedExpectedSha256, got $actualHash."
    }

    Expand-Archive -LiteralPath $archivePath -DestinationPath $extractPath
    $payloadRoot = if (Test-Path -LiteralPath (Join-Path $extractPath 'pgsql')) {
        Join-Path $extractPath 'pgsql'
    } else {
        $extractPath
    }

    foreach ($requiredFile in 'bin\initdb.exe','bin\pg_ctl.exe','bin\pg_isready.exe','bin\pg_dump.exe','bin\pg_restore.exe') {
        if (-not (Test-Path -LiteralPath (Join-Path $payloadRoot $requiredFile))) {
            throw "The verified PostgreSQL archive is missing $requiredFile."
        }
    }

    $licenseSource = @(
        (Join-Path $payloadRoot 'doc\COPYRIGHT'),
        (Join-Path $payloadRoot 'COPYRIGHT'),
        (Join-Path $payloadRoot 'server_license.txt')
    ) | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
    if (-not $licenseSource) {
        throw 'The PostgreSQL archive did not contain its COPYRIGHT license file.'
    }

    foreach ($nonRuntimePath in @(
        'pgAdmin 4',
        'StackBuilder',
        'include',
        'doc',
        'symbols',
        'pgAdmin_3rd_party_licenses.txt',
        'pgAdmin_license.txt',
        'StackBuilder_3rd_party_licenses.txt'
    )) {
        $candidate = Join-Path $payloadRoot $nonRuntimePath
        if (Test-Path -LiteralPath $candidate) {
            Remove-Item -LiteralPath $candidate -Recurse -Force
        }
    }

    New-Item -ItemType Directory -Path $destinationParent -Force | Out-Null
    if (Test-Path -LiteralPath $destinationPath) {
        Move-Item -LiteralPath $destinationPath -Destination $previousDestination
    }

    try {
        Move-Item -LiteralPath $payloadRoot -Destination $destinationPath
    } catch {
        if (Test-Path -LiteralPath $destinationPath) {
            Remove-Item -LiteralPath $destinationPath -Recurse -Force
        }

        if (Test-Path -LiteralPath $previousDestination) {
            Move-Item -LiteralPath $previousDestination -Destination $destinationPath
        }

        throw
    }

    if (Test-Path -LiteralPath $previousDestination) {
        Remove-DirectoryWithRetries $previousDestination | Out-Null
    }

    Write-Output "PostgreSQL $($descriptor.version) installed at $destinationPath with SHA-256 $actualHash"
} finally {
    if (Test-Path -LiteralPath $temporaryRoot) {
        Remove-DirectoryWithRetries $temporaryRoot | Out-Null
    }
}
