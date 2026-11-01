# Launch the local game client with the launcher-equivalent environment.
#
# Credentials are derived from the DPAPI-protected local-secrets file
# (same algorithm as the server's LocalGameLogin.FromSecrets) rather than
# hardcoded, so they can never drift from the provisioned database account.
#
# R-07: the derived password is NOT placed in the child process environment
# block (every same-user process can read a running process's env block via
# its PEB). Instead it is written to the client's own sidecar file
# local-client.env in Data\Keys -- the path that MuMain
# LocalLoginCredentials.ApplyLaunchProfile scans next to MU_CONFIG_FILE --
# locked to this user, and deleted as soon as the client exits. The same-user
# trust boundary still applies: run this only on a single-user machine, never
# under a shared/service account.
param(
    # $env:MU_SERVER_ROOT overrides the installed location. The default is
    # evaluated before local-credentials.ps1 is dot-sourced, so it cannot call
    # Get-LocalServerRoot (see Get-LocalServerRoot for the same expression).
    [string] $Root = (@($env:MU_SERVER_ROOT, 'C:\OpenMU-Local') |
        Where-Object { $_ } | Select-Object -First 1),
    [switch] $Wait
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'local-credentials.ps1')

$cred = Get-LocalGameCredential -KeysDir (Join-Path $Root 'Data\Keys')

# The client parses the sidecar as NAME=VALUE lines (see
# MuMain LocalLoginCredentials.ApplySidecar). It accepts only the MU_LOCAL_*
# launch variables and only when the process env does not already provide them,
# so a real launcher-provided value still wins.
$credentialDirectory = Join-Path $Root 'Data\Keys'
$credentialPath = Join-Path $credentialDirectory 'local-client.env'
# Clean up any stale sidecar credential file from a prior interrupted run before creating a new one
if (Test-Path -LiteralPath $credentialPath)
{
    Remove-Item -LiteralPath $credentialPath -Force -ErrorAction SilentlyContinue
}

$p = $null
try
{
    $sidecar = @(
        'MU_LOCAL_AUTO_LOGIN=1',
        ('MU_LOCAL_GAME_USERNAME=' + $cred.Username),
        ('MU_LOCAL_GAME_PASSWORD=' + $cred.Password)
    )
    [System.IO.File]::WriteAllLines($credentialPath, $sidecar, [Text.UTF8Encoding]::new($false))

    # Lock down ACL to current user identity, avoiding domain/username parsing ambiguity
    $currentUser = [System.Security.Principal.WindowsIdentity]::GetCurrent().Name
    icacls $credentialPath /inheritance:r /grant:r "${currentUser}:(R,W)" | Out-Null
    if ($LASTEXITCODE -ne 0)
    {
        icacls $credentialPath /inheritance:r /grant:r "$($env:USERNAME):(R,W)" | Out-Null
    }

    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = Join-Path $Root 'App\Game\Main.exe'
    $psi.WorkingDirectory = Join-Path $Root 'App\Game'
    $psi.Arguments = 'connect /u127.0.0.1 /p44406'
    $psi.UseShellExecute = $false
    $variables = $psi.EnvironmentVariables
    $variables['MU_SOLO_BALANCE'] = '1'
    $variables['MU_CONFIG_FILE'] = Join-Path $Root 'Data\Keys\game-config.ini'
    # Auto-login username/password come from the sidecar file above, NOT from
    # this environment block, so the derived password never sits in the child
    # process env block.
    $p = [System.Diagnostics.Process]::Start($psi)
    $p.EnableRaisingEvents = $true

    # Register-ObjectEvent works only while this PowerShell host runspace is active.
    # To prevent credential leaks when launched via short-lived non-interactive runners (CI, shortcuts, tasks),
    # spawn a detached, lightweight background watcher process that waits for the game process to exit
    # and deterministically cleans up the sidecar file.
    if ($Wait)
    {
        $p.WaitForExit()
    }
    else
    {
        # Detached watcher survives calling PowerShell script exit
        $watcherCmd = "try { `$proc = Get-Process -Id $($p.Id) -ErrorAction Stop; `$proc.WaitForExit(); } finally { Remove-Item -LiteralPath '$credentialPath' -Force -ErrorAction SilentlyContinue }"
        Start-Process -FilePath 'powershell.exe' -ArgumentList '-NoProfile', '-NonInteractive', '-WindowStyle', 'Hidden', '-Command', $watcherCmd -WindowStyle Hidden -ErrorAction SilentlyContinue | Out-Null

        # Also register the in-session event in case this host stays open
        Register-ObjectEvent -InputObject $p -EventName Exited -MessageData $credentialPath -Action {
            Remove-Item -LiteralPath $Event.MessageData -Force -ErrorAction SilentlyContinue
        } | Out-Null
    }

    Write-Output ("CLIENT_STARTED pid=" + $p.Id)
}
finally
{
    if ($p -and $p.HasExited)
    {
        Remove-Item -LiteralPath $credentialPath -Force -ErrorAction SilentlyContinue
    }
}
