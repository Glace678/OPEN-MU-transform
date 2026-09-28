$ErrorActionPreference = 'Stop'
$p = (Get-Item 'D:\openmu*\OpenMU-Android\Build-AndroidPackage.ps1' | Where-Object { Test-Path $_ } | Select-Object -First 1).FullName
Write-Output ("TARGET: " + $p)
$tokens = $null
$errs = $null
[void][System.Management.Automation.Language.Parser]::ParseFile($p, [ref]$tokens, [ref]$errs)
Write-Output ("PS VERSION: " + $PSVersionTable.PSVersion.ToString())
if ($errs.Count -eq 0) { Write-Output "PARSE: OK" }
foreach ($e in $errs) {
    Write-Output ("ERR " + $e.Extent.StartLineNumber + ":" + $e.Extent.StartColumnNumber + " " + $e.Message)
}
$bytes = [IO.File]::ReadAllBytes($p)
Write-Output ("FIRST BYTES: " + ($bytes[0..3] | ForEach-Object { $_.ToString('X2') }) -join ' ')
