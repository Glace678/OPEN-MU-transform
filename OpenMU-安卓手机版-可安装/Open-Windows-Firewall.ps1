# param() must be the first statement.
param([switch]$Force)

$ErrorActionPreference = 'Stop'
$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = [Security.Principal.WindowsPrincipal]::new($identity)
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Run this script from an Administrator PowerShell window.'
}

$ruleName = 'OpenMU Mobile LAN'

# P-04: keep exposure minimal -- Private profile + local subnet only. Refuse to
# install a rule that cannot apply while Windows labels the network Public (same
# guard as the developer firewall script), unless -Force is passed.
# Get-NetConnectionProfile exposes the category as NetworkCategory (NetProfileCategory is
# not a property of that object). Pick the profile of an interface which is actually
# classified as Private; a machine can have several profiles at once.
$connectionProfiles = @(Get-NetConnectionProfile)
$privateProfile = $connectionProfiles |
    Where-Object { $_.NetworkCategory -eq 'Private' } |
    Select-Object -First 1
if ($null -eq $privateProfile -and -not $Force) {
    $categories = ($connectionProfiles | ForEach-Object { "$($_.InterfaceAlias): $($_.NetworkCategory)" }) -join ', '
    throw "No network interface is classified as Private ($categories). The rule " +
        "applies to the Private profile only, so the mobile ports would stay blocked. " +
        "Set the relevant network to Private and re-run, or pass -Force."
}
$firewallProfile = if ($Force) { 'Any' } else { 'Private' }

# P-03: make the rule update transactional. Snapshot the existing rule set so that
# if New-NetFirewallRule fails after removal, we restore the previous rule instead
# of leaving the machine with no rule at all.
$existing = Get-NetFirewallRule -DisplayName $ruleName -ErrorAction SilentlyContinue
$hadExisting = $null -ne $existing
try {
    if ($existing) { $existing | Remove-NetFirewallRule }
    New-NetFirewallRule `
        -DisplayName $ruleName `
        -Direction Inbound `
        -Action Allow `
        -Protocol TCP `
        -LocalPort 5080,44405,44406,55901,55902,55980 `
        -RemoteAddress LocalSubnet `
        -Profile $firewallProfile | Out-Null
} catch {
    if ($hadExisting) {
        New-NetFirewallRule `
            -DisplayName $ruleName `
            -Direction Inbound `
            -Action Allow `
            -Protocol TCP `
            -LocalPort 5080,44405,44406,55901,55902,55980 `
            -RemoteAddress LocalSubnet `
            -Profile Private | Out-Null
    }
    throw "Firewall rule update failed and the previous rule was restored: $($_.Exception.Message)"
}

Write-Output 'OpenMU mobile ports are allowed from the local subnet on the Private profile.'