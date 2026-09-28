# Derives the local solo game credentials from the DPAPI-protected local-secrets
# file, using exactly the same algorithm as the server's
# LocalGameLogin.FromSecrets (see provision_secrets.ps1).
#
# Dot-source this file, then call Get-LocalGameCredential.
# Never hardcode these credentials: they are derived so that re-provisioning
# the secrets stays in sync with both the database account and the client env.

Add-Type -AssemblyName System.Security

# Installed server root. MU_SERVER_ROOT overrides the default so that a server
# installed somewhere else works without editing any script.
function Get-LocalServerRoot {
    return $env:MU_SERVER_ROOT, 'C:\OpenMU-Local' |
        Where-Object { $_ } |
        Select-Object -First 1
}

function Get-LocalSecrets {
    param(
        [string] $KeysDir = (Join-Path (Get-LocalServerRoot) 'Data\Keys')
    )
    $path = Join-Path $KeysDir 'local-secrets.dpapi'
    if (-not (Test-Path -LiteralPath $path)) {
        throw "local-secrets.dpapi not found at $path. Run provision_secrets.ps1 first."
    }
    $entropy = [System.Text.Encoding]::UTF8.GetBytes('OpenMU-Local.Secrets.v1')
    $json = [System.Text.Encoding]::UTF8.GetString(
        [System.Security.Cryptography.ProtectedData]::Unprotect(
            [System.IO.File]::ReadAllBytes($path), $entropy, 'CurrentUser'))
    return ($json | ConvertFrom-Json)
}

function Get-LocalGameCredential {
    param(
        [string] $KeysDir = (Join-Path (Get-LocalServerRoot) 'Data\Keys')
    )
    $secrets = Get-LocalSecrets -KeysDir $KeysDir
    $key = [System.Text.Encoding]::UTF8.GetBytes($secrets.AccountPassword)
    $h1 = (New-Object System.Security.Cryptography.HMACSHA256(, $key)).ComputeHash(
        [System.Text.Encoding]::UTF8.GetBytes('OpenMU-Solo.LoginName.v1'))
    $h2 = (New-Object System.Security.Cryptography.HMACSHA256(, $key)).ComputeHash(
        [System.Text.Encoding]::UTF8.GetBytes('OpenMU-Solo.LoginPassword.v1'))
    $user = 'solo' + [BitConverter]::ToString($h1).Replace('-', '').Substring(0, 6)
    $pass = [Convert]::ToBase64String($h2).Substring(0, 20).Replace('+', '-').Replace('/', '_')
    return [pscustomobject]@{ Username = $user; Password = $pass }
}
