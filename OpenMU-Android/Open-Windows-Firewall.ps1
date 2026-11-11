# param() MUST be the first statement in a PowerShell script; the previous
# version set $ErrorActionPreference above it, which made the parser fail before
# the firewall rule could ever be created (finding A-01).
param(
    [switch]$AllowPublicNetwork
)

$ErrorActionPreference = 'Stop'
$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = [Security.Principal.WindowsPrincipal]::new($identity)
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Run this script from an Administrator PowerShell window.'
}

# A rule scoped to the Private profile does nothing while Windows labels the
# network Public (the default for a fresh Wi-Fi connection). Refuse to install a
# rule that cannot apply unless the operator explicitly opts in.
$connectionProfile = Get-NetConnectionProfile | Select-Object -First 1
if ($null -ne $connectionProfile -and $connectionProfile.NetworkCategory -ne 'Private') {
    if (-not $AllowPublicNetwork) {
        throw "The current network '$($connectionProfile.Name)' is a " +
            "$($connectionProfile.NetworkCategory) network. The mobile ports stay " +
            "firewalled. Set the network to Private (Settings > Network > Properties > " +
            "Private) and re-run, or pass -AllowPublicNetwork to opt in explicitly. " +
            "Do not run on an untrusted public network."
    }

    Write-Warning ("WARNING: the current network is $($connectionProfile.NetworkCategory). The rule below allows inbound " +
        "mobile ports (5080,44405,44406,55901,55902,55980) from LocalSubnet on this " +
        "$($connectionProfile.NetworkCategory) network, so ANY device on the same Wi-Fi can reach them. Only continue on a " +
        "trusted network you control.")
}

$ruleName = 'OpenMU Mobile LAN'
$existing = @(Get-NetFirewallRule -DisplayName $ruleName -ErrorAction SilentlyContinue)

# F1 (M1-04 / M1-07): never default to the Any profile. The rule is scoped to the
# Private profile unless the operator explicitly passes -AllowPublicNetwork, and
# the remote address is always LocalSubnet (never Any).
$firewallProfile = if ($AllowPublicNetwork) { 'Any' } else { 'Private' }

$created = New-NetFirewallRule `
    -DisplayName $ruleName `
    -Direction Inbound `
    -Action Allow `
    -Protocol TCP `
    -LocalPort 5080,44405,44406,55901,55902,55980 `
    -RemoteAddress LocalSubnet `
    -Profile $firewallProfile
if (-not $created) {
    throw "Failed to create firewall rule '$ruleName'."
}
if ($existing.Count -gt 0) {
    $existing | Remove-NetFirewallRule
}

if ($firewallProfile -eq 'Any') {
    Write-Output 'OpenMU mobile ports are allowed from the local subnet on any network profile (-AllowPublicNetwork).'
} else {
    Write-Output 'OpenMU mobile ports are allowed only from the local subnet on Private networks.'
}
