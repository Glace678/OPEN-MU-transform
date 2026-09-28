[CmdletBinding()]
param([Parameter(Mandatory)][string]$PackageDirectory)

$ErrorActionPreference = 'Stop'
if (-not $IsLinux -and -not $IsMacOS) { throw 'Native dependencies must be packaged on Linux or macOS.' }
$root = (Resolve-Path -LiteralPath $PackageDirectory).Path
$native = Join-Path $root 'Runtime/Native'
[void][IO.Directory]::CreateDirectory($native)
$queue = [Collections.Generic.Queue[object]]::new()
$copied = [Collections.Generic.Dictionary[string,string]]::new([StringComparer]::Ordinal)
$report = [Collections.Generic.List[object]]::new()

function Get-NativeFormat([string]$Path) {
    $stream = [IO.File]::OpenRead($Path)
    try {
        $header = [byte[]]::new(4)
        if ($stream.Read($header, 0, 4) -ne 4) { return '' }
        switch ([Convert]::ToHexString($header)) {
            '7F454C46' { return 'elf' }
            'CFFAEDFE' { return 'macho' }
        }
        return ''
    } finally { $stream.Dispose() }
}

function Invoke-Native([string]$Command, [string[]]$Arguments) {
    $result = & $Command @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) { throw "$Command failed for $($Arguments -join ' '): $result" }
    return $result
}

function Add-Dependency([string]$Source) {
    $name = [IO.Path]::GetFileName($Source)
    if ($copied.ContainsKey($name)) {
        if ((Get-FileHash $Source -Algorithm SHA256).Hash -ne $copied[$name]) {
            throw "Conflicting native dependencies share a filename: $name"
        }
        return (Join-Path $native $name)
    }
    $destination = Join-Path $native $name
    $hash = (Get-FileHash $Source -Algorithm SHA256).Hash
    Copy-Item -LiteralPath $Source -Destination $destination
    $copied.Add($name, $hash)
    $queue.Enqueue(@{ source = $Source; target = $destination })
    $report.Add([ordered]@{ file = $name; source = $Source; originalSha256 = $hash })
    return $destination
}

foreach ($file in Get-ChildItem -LiteralPath $root -File -Recurse) {
    $format = Get-NativeFormat $file.FullName
    if (($IsMacOS -and $format -eq 'macho') -or ($IsLinux -and $format -eq 'elf')) {
        $queue.Enqueue(@{ source = $file.FullName; target = $file.FullName })
    }
}

if ($IsLinux) {
    # .NET loads these at runtime, so ldd alone does not find them.
    $catalog = Invoke-Native 'ldconfig' @('-p')
    foreach ($name in @('libicui18n.so', 'libicuuc.so', 'libicudata.so', 'libssl.so.3', 'libcrypto.so.3')) {
        $match = $catalog | Where-Object { $_ -match "^\s*$([regex]::Escape($name))(?:\.[0-9]+)?\s.*=>\s+(\S+)$" } | Select-Object -First 1
        if (-not $match) { throw "Missing runtime dependency: $name" }
        $source = ($match -split '=>', 2)[1].Trim()
        [void](Add-Dependency $source)
    }
}

while ($queue.Count -gt 0) {
    $item = $queue.Dequeue()
    if ($IsMacOS) {
        $dependencies = @(Invoke-Native 'otool' @('-L', $item.source) | Select-Object -Skip 1)
        foreach ($line in $dependencies) {
            $dependency = ($line.Trim() -split ' \(compatibility version', 2)[0]
            if ($dependency.StartsWith('/usr/lib/') -or $dependency.StartsWith('/System/Library/')) { continue }
            # Package-relative dependencies are already staged by dotnet publish.
            if ($dependency.StartsWith('@loader_path/') -or $dependency.StartsWith('@executable_path/')) { continue }
            if ($dependency.StartsWith('@rpath/')) {
                $local = Join-Path (Split-Path $item.source -Parent) $dependency.Substring(7)
                if (Test-Path -LiteralPath $local) { continue }
                throw "Unresolved Mach-O rpath dependency: $dependency in $($item.source)"
            }
            if (-not [IO.Path]::IsPathRooted($dependency)) { throw "Unknown Mach-O dependency: $dependency" }
            if ($dependency -eq $item.source) { continue }
            $destination = Add-Dependency $dependency
            $relative = [IO.Path]::GetRelativePath((Split-Path $item.target -Parent), $destination)
            [void](Invoke-Native 'install_name_tool' @('-change', $dependency, "@loader_path/$relative", $item.target))
        }
        # Rewriting load commands invalidates existing ad-hoc signatures.
        # This is local execution signing only, not Developer ID or notarization.
        [void](Invoke-Native 'codesign' @('--force', '--sign', '-', $item.target))
    } else {
        $dependencies = & ldd $item.source 2>&1
        if ($LASTEXITCODE -ne 0) { continue } # Static PostgreSQL helpers have no dependency table.
        foreach ($line in $dependencies) {
            if ($line -match '=>\s+not found') { throw "Unresolved ELF dependency in $($item.source): $line" }
            if ($line -notmatch '^\s*(\S+)\s+=>\s+(/\S+)\s+\(') { continue }
            $name = $Matches[1]
            $source = $Matches[2]
            # Keep libc, the loader and the host graphics driver on the host.
            if ($name -match '^(lib(c|m|dl|pthread|rt|resolv|util|nss_[^.]+)\.so|lib(GL|GLX|GLdispatch|EGL|drm|gbm)\.so)') { continue }
            [void](Add-Dependency $source)
        }
        $relative = [IO.Path]::GetRelativePath((Split-Path $item.target -Parent), $native)
        $existing = (Invoke-Native 'patchelf' @('--print-rpath', $item.target)) -join ''
        $rpath = '$ORIGIN:$ORIGIN/' + $relative
        if ($existing) { $rpath += ':' + $existing }
        [void](Invoke-Native 'patchelf' @('--set-rpath', $rpath, $item.target))
    }
}
$report | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $root 'Licenses/native-dependencies.json') -Encoding utf8NoBOM
Write-Output "Bundled $($report.Count) native dependencies."
