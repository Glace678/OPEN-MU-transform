<#
.SYNOPSIS
    Provisions the local solo game account in PostgreSQL.
.DESCRIPTION
    Generates a parameter-free SQL script using BCrypt hashing matching SoloAccountInitializer.
    Credentials are derived from DPAPI-protected local secrets via HMAC-SHA256 to prevent drifting.
.OUTPUTS
    SQL_WRITTEN=[Path to generated SQL file]
    ID=[Generated Account GUID]
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'local-credentials.ps1')

$KeysDir  = Join-Path (Get-LocalServerRoot) 'Data\Keys'
$OutSql   = Join-Path $KeysDir 'provision_account.sql'

$cred = Get-LocalGameCredential -KeysDir $KeysDir
$Username = $cred.Username
$Password = $cred.Password

# The BCrypt.Net-Next.dll shipped with the server is .NET 6-targeted, which the
# PowerShell 5.1 AppDomain cannot load. lib/ holds the net48 build plus its
# System.Memory/Buffers/Unsafe dependencies; those three are not in the PS 5.1
# AppDomain either, so resolve them from the same folder.
$LibDir = Join-Path $PSScriptRoot 'lib'
$onResolve = {
    param($sender, $e)
    foreach ($name in 'System.Memory', 'System.Buffers', 'System.Runtime.CompilerServices.Unsafe') {
        if ($e.Name -like ($name + '*')) {
            $p = Join-Path $LibDir ($name + '.dll')
            if (Test-Path -LiteralPath $p) {
                return [System.Reflection.Assembly]::LoadFrom($p)
            }
        }
    }
    return $null
}
[System.AppDomain]::CurrentDomain.add_AssemblyResolve($onResolve)
try {
    Add-Type -Path (Join-Path $LibDir 'BCrypt.Net-Next.dll')

    $hash = [BCrypt.Net.BCrypt]::HashPassword($Password)
    if (-not [BCrypt.Net.BCrypt]::Verify($Password, $hash)) {
        throw 'BCrypt round-trip verification failed.'
    }
} finally {
    [System.AppDomain]::CurrentDomain.remove_AssemblyResolve($onResolve)
}

# Validate against SoloAccountInitializer policy.
if ($Username.Length -lt 3 -or $Username.Length -gt 10) { throw 'bad username length' }
if ($Password.Length -lt 12 -or $Password.Length -gt 20) { throw 'bad password length' }

$id  = [Guid]::NewGuid().ToString()
$now = (Get-Date).ToUniversalTime().ToString('yyyy-MM-dd HH:mm:ss.ffffffZ')

# Escape single quotes (none expected for BCrypt hashes, but be safe).
$h = $hash.Replace("'", "''")
$u = $Username.Replace("'", "''")

$sql = @"
INSERT INTO data."Account"
  ("Id","LoginName","PasswordHash","SecurityCode","EMail","RegistrationDate","State","TimeZone","VaultPassword","IsVaultExtended","IsTemplate","LanguageIsoCode","IsBot")
VALUES
  ('$id','$u','$h','','','$now',0,0,'',false,false,'zh',false)
ON CONFLICT ("LoginName") DO NOTHING;
"@

[System.IO.Directory]::CreateDirectory($KeysDir) | Out-Null
[System.IO.File]::WriteAllText($OutSql, $sql, [System.Text.UTF8Encoding]::new($false))
$identity = [System.Security.Principal.WindowsIdentity]::GetCurrent()
icacls $OutSql /inheritance:r /grant:r "*$($identity.User.Value):(R,W)" | Out-Null
if ($LASTEXITCODE -ne 0) {
    icacls $OutSql /inheritance:r /grant:r "$($identity.Name):(R,W)" | Out-Null
}
if ($LASTEXITCODE -ne 0) {
    Remove-Item -LiteralPath $OutSql -Force -ErrorAction SilentlyContinue
    throw "Failed to restrict ACL on generated SQL file: $OutSql"
}
Write-Output ("SQL_WRITTEN=" + $OutSql)
Write-Output ("ID=" + $id)
