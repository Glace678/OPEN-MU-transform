$ErrorActionPreference = 'Stop'
param(
    [switch]$Force
)
$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = [Security.Principal.WindowsPrincipal]::new($identity)
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Run this script from an Administrator PowerShell window.'
}

$ruleName = 'OpenMU Mobile LAN'
$existing = Get-NetFirewallRule -DisplayName $ruleName -ErrorAction SilentlyContinue
if ($existing) {
    $existing | Remove-NetFirewallRule
}

# A rule scoped to the Private profile does nothing while Windows labels the
# network Public (the default for a fresh Wi-Fi connection). The ports then stay
# blocked and the phone cannot connect, but this script would have reported
# success anyway. Refuse to install a rule that cannot apply.
$connectionProfile = Get-NetConnectionProfile | Select-Object -First 1
if ($null -ne $connectionProfile -and $connectionProfile.NetProfileCategory -ne 'Private') {
    throw "The current network '$($connectionProfile.Name)' is a " +
        "$($connectionProfile.NetProfileCategory) network. The firewall rule " +
        "applies to the Private profile only, so the mobile ports would stay " +
        "blocked. Set the network to Private (Settings > Network > Properties > " +
        "Private) and re-run, or pass -Force to install the rule anyway."
}
if ($Force) {
    $firewallProfile = 'Any'
} else {
    $firewallProfile = 'Private'
}

New-NetFirewallRule `
    -DisplayName $ruleName `
    -Direction Inbound `
    -Action Allow `
    -Protocol TCP `
    -LocalPort 5080,44405,44406,55901,55902,55980 `
    -RemoteAddress LocalSubnet `
    -Profile $firewallProfile | Out-Null

if ($firewallProfile -eq 'Any') {
    Write-Output 'OpenMU mobile ports are allowed from the local subnet on any network profile (-Force).'
} else {
    Write-Output 'OpenMU mobile ports are allowed only from the local subnet on Private networks.'
}
