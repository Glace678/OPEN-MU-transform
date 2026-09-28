# Launch the local game client with the launcher-equivalent environment.
#
# Credentials are derived from the DPAPI-protected local-secrets file
# (same algorithm as the server's LocalGameLogin.FromSecrets) rather than
# hardcoded, so they can never drift from the provisioned database account.
# They are still passed to the client via environment variables because the
# client reads them from its environment block (see MU_LOCAL_GAME_*).
param(
    # $env:MU_SERVER_ROOT overrides the installed location. The default is
    # evaluated before local-credentials.ps1 is dot-sourced, so it cannot call
    # Get-LocalServerRoot (see Get-LocalServerRoot for the same expression).
    [string] $Root = (@($env:MU_SERVER_ROOT, 'C:\OpenMU-Local') |
        Where-Object { $_ } | Select-Object -First 1)
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'local-credentials.ps1')

$cred = Get-LocalGameCredential -KeysDir (Join-Path $Root 'Data\Keys')

$psi = New-Object System.Diagnostics.ProcessStartInfo
$psi.FileName = Join-Path $Root 'App\Game\Main.exe'
$psi.WorkingDirectory = Join-Path $Root 'App\Game'
$psi.Arguments = 'connect /u127.0.0.1 /p44406'
$psi.UseShellExecute = $false
$env = $psi.EnvironmentVariables
$env['MU_SOLO_BALANCE'] = '1'
$env['MU_CONFIG_FILE'] = Join-Path $Root 'Data\Keys\game-config.ini'
$env['MU_LOCAL_AUTO_LOGIN'] = '1'
$env['MU_LOCAL_GAME_USERNAME'] = $cred.Username
$env['MU_LOCAL_GAME_PASSWORD'] = $cred.Password
$p = [System.Diagnostics.Process]::Start($psi)
Write-Output ("CLIENT_STARTED pid=" + $p.Id)
