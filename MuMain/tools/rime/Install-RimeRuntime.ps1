[CmdletBinding()]
param(
    [ValidateSet('x86', 'x64')]
    [string] $Architecture = 'x86',

    [string] $Destination
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$scriptRoot = Split-Path -Parent $PSCommandPath
$repositoryRoot = Resolve-Path (Join-Path $scriptRoot '..\..')
$manifestPath = Join-Path $scriptRoot 'runtime.json'
$manifest = Get-Content -Raw -LiteralPath $manifestPath | ConvertFrom-Json
$asset = $manifest.architectures.$Architecture
if ($null -eq $asset) {
    throw "No librime asset is configured for architecture '$Architecture'."
}

if ([string]::IsNullOrWhiteSpace($Destination)) {
    $Destination = Join-Path $repositoryRoot "src\third_party\librime\runtime\$Architecture"
}
$destinationPath = [IO.Path]::GetFullPath($Destination)

function Find-SevenZip {
    $command = Get-Command 7z, 7za, 7zz -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($null -ne $command) {
        return $command.Source
    }

    $candidates = @()
    if (${env:ProgramFiles}) {
        $candidates += Join-Path ${env:ProgramFiles} '7-Zip\7z.exe'
    }
    if (${env:ProgramFiles(x86)}) {
        $candidates += Join-Path ${env:ProgramFiles(x86)} '7-Zip\7z.exe'
        $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
        if (Test-Path -LiteralPath $vswhere) {
            $installations = & $vswhere -products '*' -property installationPath
            foreach ($installation in $installations) {
                $candidates += Join-Path $installation 'Common7\IDE\Extensions\Microsoft\Maui\Maui.VisualStudio\7-Zip\7z.exe'
            }
        }
    }

    foreach ($candidate in $candidates) {
        if ($candidate -and (Test-Path -LiteralPath $candidate)) {
            return $candidate
        }
    }
    throw '7-Zip was not found. Install 7-Zip or Visual Studio with the MAUI tooling component.'
}

function Expand-RimeArchive([string] $ArchivePath, [string] $ExtractPath) {
    # Windows 10 and 11 ship bsdtar, which can read the official .7z asset.
    $tar = Get-Command tar.exe, tar -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($null -ne $tar) {
        & $tar.Source -xf $ArchivePath -C $ExtractPath 'dist/lib/rime.dll' 'version-info.txt'
        if ($LASTEXITCODE -eq 0 -and (Test-Path -LiteralPath (Join-Path $ExtractPath 'dist\lib\rime.dll'))) {
            return
        }
    }

    $sevenZip = Find-SevenZip
    & $sevenZip x $ArchivePath "-o$ExtractPath" 'dist\lib\rime.dll' 'version-info.txt' -y | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "7-Zip failed with exit code $LASTEXITCODE."
    }
}

$temporaryRoot = Join-Path ([IO.Path]::GetTempPath()) ("openmu-rime-" + [guid]::NewGuid().ToString('N'))
$archivePath = Join-Path $temporaryRoot 'librime.7z'
$extractPath = Join-Path $temporaryRoot 'extract'

try {
    New-Item -ItemType Directory -Force -Path $extractPath | Out-Null
    Write-Host "Downloading librime $($manifest.version) for $Architecture..."
    Invoke-WebRequest -UseBasicParsing -Uri $asset.url -OutFile $archivePath

    $actualHash = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash.ToLowerInvariant()
    $expectedHash = ([string]$asset.sha256).ToLowerInvariant()
    if ($actualHash -ne $expectedHash) {
        throw "librime archive SHA-256 mismatch. Expected $expectedHash; got $actualHash."
    }

    Expand-RimeArchive -ArchivePath $archivePath -ExtractPath $extractPath

    $runtimeDll = Join-Path $extractPath 'dist\lib\rime.dll'
    if (-not (Test-Path -LiteralPath $runtimeDll)) {
        throw 'The verified librime archive did not contain dist/lib/rime.dll.'
    }

    New-Item -ItemType Directory -Force -Path $destinationPath | Out-Null
    Copy-Item -LiteralPath $runtimeDll -Destination (Join-Path $destinationPath 'rime.dll') -Force
    $versionInfo = Join-Path $extractPath 'version-info.txt'
    if (Test-Path -LiteralPath $versionInfo) {
        Copy-Item -LiteralPath $versionInfo -Destination (Join-Path $destinationPath 'version-info.txt') -Force
    }
    Set-Content -LiteralPath (Join-Path $destinationPath 'archive.sha256') -Encoding ascii -NoNewline -Value $actualHash

    Write-Host "Installed verified librime runtime to $destinationPath"
}
finally {
    if (Test-Path -LiteralPath $temporaryRoot) {
        Remove-Item -LiteralPath $temporaryRoot -Recurse -Force
    }
}
