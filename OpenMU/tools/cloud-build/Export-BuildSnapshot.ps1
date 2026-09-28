[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$OutputDirectory
)

$ErrorActionPreference = 'Stop'
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$output = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $output) { throw "Snapshot output must be a new directory: $output" }
[void][IO.Directory]::CreateDirectory($output)
[void][IO.Directory]::CreateDirectory((Join-Path $output 'snapshots'))
[void][IO.Directory]::CreateDirectory((Join-Path $output '.github/workflows'))

$sources = [ordered]@{}
foreach ($name in @('MuMain', 'OpenMU')) {
    $root = Join-Path $workspace $name
    $revision = (& git -C $root rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0) { throw "Cannot read source revision: $name" }
    $changed = @(& git -C $root -c core.quotepath=false ls-files --modified --others --exclude-standard)
    if ($LASTEXITCODE -ne 0) { throw "Cannot enumerate source changes: $name" }
    # These source assets are intentionally ignored by broad upstream *.txt rules.
    $extra = if ($name -eq 'OpenMU') {
        @(Get-ChildItem -LiteralPath (Join-Path $root 'tools/local-package') -File -Filter '*.txt')
    } else {
        @(Get-ChildItem -LiteralPath (Join-Path $root 'src/third_party/librime') -File -Recurse |
            Where-Object FullName -NotMatch '[/\\]runtime[/\\]')
    }
    $changed += @($extra | ForEach-Object { [IO.Path]::GetRelativePath($root, $_.FullName).Replace('\', '/') })
    $files = @($changed | Sort-Object -Unique)
    $archivePath = Join-Path $output "snapshots/$name.zip"
    $archive = [IO.Compression.ZipFile]::Open($archivePath, [IO.Compression.ZipArchiveMode]::Create)
    $entries = [Collections.Generic.List[object]]::new()
    try {
        foreach ($relative in $files) {
            if ($relative -match '(^|/)(node_modules|obj|out|artifacts|runtime|\.git|Keys|Logs|Backups|PostgreSQL)(/|$)' -or
                $relative -match '\.(exe|dll|pdb|log|db|dump|zip|pfx|pem|key|dpapi)$' -or
                ($relative -match '(^|/)bin/' -and $relative -notmatch '^src/bin/(fonts/|config\.ini\.template$)')) {
                throw "Refusing a generated or private file in the source snapshot: $name/$relative"
            }
            $path = [IO.Path]::GetFullPath((Join-Path $root $relative))
            if (-not $path.StartsWith($root + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
                throw "Source path escaped the repository: $relative"
            }
            if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
                throw "Deleted or non-file changes require explicit snapshot support: $relative"
            }
            if ((Get-Item -LiteralPath $path).LinkType) { throw "Linked source file: $relative" }
            if ([IO.Path]::GetExtension($path) -notin @('.otf', '.ttf')) {
                $text = [IO.File]::ReadAllText($path)
                if ($text -match 'github_pat_[A-Za-z0-9_]{20,}|gh[pousr]_[A-Za-z0-9]{20,}|-----BEGIN (RSA |EC |OPENSSH )?PRIVATE KEY-----') {
                    throw "Credential pattern found in source; upload refused: $name/$relative"
                }
            }
            $entry = $archive.CreateEntry($relative, [IO.Compression.CompressionLevel]::Optimal)
            # ZIP stores wall-clock time without an offset. A fixed UTC epoch
            # prevents Windows timestamps appearing hours in the future on CI.
            $entry.LastWriteTime = [DateTimeOffset]::FromUnixTimeSeconds(946684800)
            $inputStream = [IO.File]::OpenRead($path)
            $outputStream = $entry.Open()
            try { $inputStream.CopyTo($outputStream) }
            finally {
                $outputStream.Dispose()
                $inputStream.Dispose()
            }
            $entries.Add([ordered]@{ path = $relative; sha256 = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash })
        }
    } finally { $archive.Dispose() }
    $sources[$name] = [ordered]@{
        repository = if ($name -eq 'MuMain') { 'sven-n/MuMain' } else { 'MUnique/OpenMU' }
        revision = $revision
        archiveSha256 = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash
        files = $entries
    }
}
$sources | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $output 'sources.json') -Encoding utf8NoBOM
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'desktop-unsigned.yml') -Destination (Join-Path $output '.github/workflows/desktop-unsigned.yml')
Write-Output "Sanitized source snapshot: $output"
$sources.GetEnumerator() | ForEach-Object { Write-Output "$($_.Key): $($_.Value.files.Count) changed source files" }
