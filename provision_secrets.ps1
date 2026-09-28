# Pre-provision OpenMU-Local DPAPI secrets so the launcher skips the
# interactive first-run admin-password dialog.
# Body is ASCII-only; the server directory is passed as a UTF-16 arg.
param(
    [Parameter(Mandatory = $true)]
    [string] $ServerDir,
    [Parameter(Mandatory = $true)]
    [string] $AdminPassword
)
Add-Type -AssemblyName System.Security

$keysDir = Join-Path $ServerDir 'Data\Keys'
New-Item -ItemType Directory -Force -Path $keysDir | Out-Null

$rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
function New-Secret {
    $b = New-Object byte[] 32
    $rng.GetBytes($b)
    [Convert]::ToBase64String($b).TrimEnd('=').Replace('+', '-').Replace('/', '_')
}

$secrets = [ordered]@{
    DatabaseAdminPassword = (New-Secret)
    ConfigurationPassword = (New-Secret)
    AccountPassword       = (New-Secret)
    FriendPassword        = (New-Secret)
    GuildPassword         = (New-Secret)
    AdminPanelPassword    = $AdminPassword
}

$json    = ($secrets | ConvertTo-Json -Compress)
$bytes   = [System.Text.Encoding]::UTF8.GetBytes($json)
$entropy = [System.Text.Encoding]::UTF8.GetBytes('OpenMU-Local.Secrets.v1')
$protected = [System.Security.Cryptography.ProtectedData]::Protect(
    $bytes, $entropy, [System.Security.Cryptography.DataProtectionScope]::CurrentUser)
$secretsPath = Join-Path $keysDir 'local-secrets.dpapi'
[System.IO.File]::WriteAllBytes($secretsPath, $protected)

# Derive the solo game login (same algorithm as LocalGameLogin.FromSecrets)
$key = [System.Text.Encoding]::UTF8.GetBytes($secrets.AccountPassword)
$hmac1 = New-Object System.Security.Cryptography.HMACSHA256(, $key)
$h1 = $hmac1.ComputeHash([System.Text.Encoding]::UTF8.GetBytes('OpenMU-Solo.LoginName.v1'))
$hmac2 = New-Object System.Security.Cryptography.HMACSHA256(, $key)
$h2 = $hmac2.ComputeHash([System.Text.Encoding]::UTF8.GetBytes('OpenMU-Solo.LoginPassword.v1'))
$gameUser = 'solo' + [BitConverter]::ToString($h1).Replace('-', '').Substring(0, 6)
$gamePass = [Convert]::ToBase64String($h2).Substring(0, 20).Replace('+', '-').Replace('/', '_')

Write-Output ("SECRETS_FILE=" + $secretsPath)
Write-Output ("FILE_SIZE=" + (Get-Item -LiteralPath $secretsPath).Length)
Write-Output "GAME_USER=$gameUser"
Write-Output "GAME_PASS=$gamePass"
Write-Output "ADMIN_USER=localadmin"
# Do not echo the admin password; the caller already knows what it passed in.

# Round-trip verification
$check = [System.Security.Cryptography.ProtectedData]::Unprotect(
    [System.IO.File]::ReadAllBytes($secretsPath),
    $entropy, [System.Security.Cryptography.DataProtectionScope]::CurrentUser)
$rt = [System.Text.Encoding]::UTF8.GetString($check) | ConvertFrom-Json
$ok = ($rt.DatabaseAdminPassword.Length -ge 32) -and
      ($rt.ConfigurationPassword.Length -ge 32) -and
      ($rt.AccountPassword.Length -ge 32) -and
      ($rt.FriendPassword.Length -ge 32) -and
      ($rt.GuildPassword.Length -ge 32) -and
      ($rt.AdminPanelPassword.Length -ge 12)
if ($ok) { Write-Output "ROUNDTRIP_OK" } else { Write-Output "ROUNDTRIP_FAIL"; exit 1 }
