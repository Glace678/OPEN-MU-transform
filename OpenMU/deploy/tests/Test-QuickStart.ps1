<# No Docker daemon or real database is used. All startup calls are mocked. #>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$quickStart = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\quick-start.ps1'))
$tempBase = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
$testRoot = Join-Path $tempBase ('openmu-deploy-test-' + [Guid]::NewGuid().ToString('N'))
$script:Calls = [Collections.Generic.List[object]]::new()
$script:Checks = 0
$script:FailureVerb = $null
$global:OpenMuDeploymentTestState = [pscustomobject]@{ Calls = $script:Calls; FailureVerb = $null }
$originalDocker = Get-Item Function:global:docker -ErrorAction SilentlyContinue
$originalDbPassword = [Environment]::GetEnvironmentVariable('DB_ADMIN_PW', 'Process')

function Assert-True([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
    $script:Checks++
}

function Assert-Rejected([scriptblock]$Action, [string]$Message) {
    $rejected = $false
    try { & $Action | Out-Null } catch { $rejected = $true }
    Assert-True $rejected $Message
}

function global:docker {
    $global:OpenMuDeploymentTestState.Calls.Add(@($args))
    if ([Environment]::GetEnvironmentVariable('DB_ADMIN_PW', 'Process')) {
        throw 'Inherited credentials were not cleared before Compose.'
    }
    $failureVerb = $global:OpenMuDeploymentTestState.FailureVerb
    $global:LASTEXITCODE = $(if ($failureVerb -and $args -contains $failureVerb) { 42 } else { 0 })
}

try {
    $emptyConfig = Join-Path $testRoot 'not-created'
    $plan = & $quickStart -DryRun -ConfigDirectory $emptyConfig
    Assert-True ($plan.ImageMode -eq 'local-source') 'Local source must be the default.'
    Assert-True ($plan.GameBind -eq '127.0.0.1') 'Local deployment must not expose game ports.'
    Assert-True ($plan.AdminBind -eq '127.0.0.1') 'Administration must remain loopback-only.'
    Assert-True ($plan.AdvertisedAddress -eq '127.0.0.1') 'Local clients must receive a reachable loopback IPv4.'
    Assert-True ($plan.Commands.Count -eq 3) 'Expected config, build, and up commands.'
    Assert-True (($plan.Commands[1] -join ' ') -match 'build openmu-startup$') 'Default startup must build the local image.'
    Assert-True (($plan.Commands[0] -join ' ') -match 'docker-compose.local.yml') 'The dedicated local overlay must be selected.'
    Assert-True (($plan.Commands[0] -join ' ') -match '--project-name all-in-one') 'Compose volume identities must remain stable.'
    Assert-True (-not (($plan.Commands | ConvertTo-Json -Depth 5) -match 'pull|push|-reinit|-demo|docker-compose.override')) 'Default startup must not pull upstream, upload, drop data, or load development port overrides.'
    Assert-True (-not (Test-Path -LiteralPath $testRoot)) 'DryRun must not create directories or credentials.'
    Assert-True ($script:Calls.Count -eq 0) 'DryRun must not call Docker.'

    $cloudPlan = & $quickStart -DryRun -ConfigDirectory $emptyConfig -PublicAddress '203.0.113.25' -WebPort 8088
    Assert-True ($cloudPlan.GameBind -eq '0.0.0.0') 'An explicit advertised address must enable remote game ports.'
    Assert-True ($cloudPlan.AdvertisedAddress -eq '203.0.113.25') 'The exact advertised IPv4 must reach RESOLVE_IP.'
    Assert-True ($cloudPlan.WebPort -eq '8088') 'The requested admin port must be preserved.'
    Assert-True ($cloudPlan.AdminBind -eq '127.0.0.1') 'Cloud deployment must not expose unauthenticated HTTP administration.'

    $portalPlan = & $quickStart -DryRun -ConfigDirectory $emptyConfig -PublicAddress '203.0.113.25' -PortalDomain 'accounts.example.com'
    Assert-True ($portalPlan.PlayerPortal -eq 'https://accounts.example.com') 'The public player URL must use HTTPS.'
    Assert-True (($portalPlan.Commands[0] -join ' ') -match 'docker-compose.portal.yml') 'Portal option must select the separate allowlisted overlay.'
    Assert-True ($portalPlan.AdminBind -eq '127.0.0.1') 'The player portal must not expose the admin site.'
    Assert-Rejected { & $quickStart -DryRun -ConfigDirectory $emptyConfig -PortalDomain 'accounts.example.com' } 'Public portal without a reachable game address accepted.'
    Assert-Rejected { & $quickStart -DryRun -ConfigDirectory $emptyConfig -PublicAddress '203.0.113.25' -PortalDomain 'accounts.example.com' -PublishedImage } 'Portal must not use an upstream image lacking recovery endpoints.'
    foreach ($domain in @('localhost', '127.0.0.1', 'https://accounts.example.com', 'accounts.example.com:443', 'a..com', 'a.local', 'a.com/path')) {
        Assert-Rejected { & $quickStart -DryRun -ConfigDirectory $emptyConfig -PortalDomain $domain } "Unsafe portal domain accepted: $domain"
    }

    foreach ($invalidAddress in @('127.0.0.1', '0.0.0.0', '256.1.1.1', '::1', 'https://example.com', '224.1.2.3')) {
        Assert-Rejected { & $quickStart -DryRun -ConfigDirectory $emptyConfig -PublicAddress $invalidAddress } "Invalid address accepted: $invalidAddress"
    }
    Assert-Rejected { & $quickStart -DryRun -ConfigDirectory $emptyConfig -Build -PublishedImage } 'Contradictory image flags were accepted.'
    Assert-Rejected { & $quickStart -DryRun -ConfigDirectory $emptyConfig -AdminPassword 'secret$interpolation' } 'Unsafe dotenv password was accepted.'
    $stopPlan = & $quickStart -DryRun -ConfigDirectory $emptyConfig -Down
    Assert-True ($stopPlan.Commands.Count -eq 0) 'Stopping before first startup must be a no-op.'

    $publishedPlan = & $quickStart -DryRun -ConfigDirectory $emptyConfig -PublishedImage
    Assert-True ($publishedPlan.ImageMode -eq 'published-upstream') 'The explicitly selected upstream image must be marked.'
    Assert-True (($publishedPlan.Commands[1] -join ' ') -match 'pull openmu-startup$') 'Only explicit upstream mode may pull OpenMU.'
    Assert-True (-not (($publishedPlan.Commands[0] -join ' ') -match 'docker-compose.local')) 'Upstream mode must not silently mix the local image.'

    [Environment]::SetEnvironmentVariable('DB_ADMIN_PW', 'inherited-not-the-saved-password', 'Process')
    $config = Join-Path $testRoot 'mock-start'
    & $quickStart -ConfigDirectory $config -PublicAddress '203.0.113.25' -PortalDomain 'accounts.example.com'
    $envPath = Join-Path $config '.env'
    $savedBytes = [IO.File]::ReadAllBytes($envPath)
    $savedText = [IO.File]::ReadAllText($envPath)
    Assert-True ($savedText -match '(?m)^OPENMU_ADMIN_PASSWORD=[A-Za-z0-9_-]{32}\r?$') 'Generated admin password must have 192 bits of randomness and be dotenv-safe.'
    Assert-True ($savedText -match '(?m)^DB_ADMIN_PW=[A-Za-z0-9_-]{32}\r?$') 'Generated database password must be dotenv-safe.'
    Assert-True ($savedText -match '(?m)^RESOLVE_IP=203\.0\.113\.25\r?$') 'The explicit shared-server address must be persisted.'
    Assert-True ($savedText -match '(?m)^OPENMU_GAME_BIND=0\.0\.0\.0\r?$') 'Remote game binding must be persisted.'
    Assert-True ($script:Calls.Count -eq 4) 'Mock startup must call version, config, build, up.'
    Assert-True ([Environment]::GetEnvironmentVariable('DB_ADMIN_PW', 'Process') -eq 'inherited-not-the-saved-password') 'Caller environment must be restored.'
    & $quickStart -ConfigDirectory $config
    Assert-True ([Convert]::ToBase64String($savedBytes) -eq [Convert]::ToBase64String([IO.File]::ReadAllBytes($envPath))) 'Repeated startup must preserve credentials byte-for-byte.'
    $existingPlan = & $quickStart -DryRun -ConfigDirectory $config
    Assert-True ($existingPlan.AdvertisedAddress -eq '203.0.113.25') 'Reruns must preserve shared-server addressing.'
    Assert-True ($existingPlan.PlayerPortal -eq 'https://accounts.example.com') 'Reruns must preserve the player portal.'
    $portalStop = & $quickStart -DryRun -ConfigDirectory $config -Down
    Assert-True (($portalStop.Commands[0] -join ' ') -match 'docker-compose.portal.yml') 'Stopping must include the previously enabled portal.'
    Assert-True (-not $existingPlan.WouldCreateCredentials) 'Existing credentials must not be regenerated.'
    Assert-True (-not (($existingPlan | ConvertTo-Json -Depth 5) -match 'inherited-not-the-saved-password|OPENMU_ADMIN_PASSWORD=')) 'DryRun must not disclose secrets.'

    $global:OpenMuDeploymentTestState.FailureVerb = 'build'
    $script:Calls.Clear()
    Assert-Rejected { & $quickStart -ConfigDirectory $config } 'A failed build must abort startup.'
    Assert-True (-not (@($script:Calls | Where-Object { $_ -contains 'up' }).Count)) 'A failed build must never start containers.'
    $global:OpenMuDeploymentTestState.FailureVerb = $null
    $script:Calls.Clear()
    [IO.File]::WriteAllText($envPath, $savedText.Replace('DB_NAME=openmu', 'DB_NAME=other'))
    Assert-Rejected { & $quickStart -DryRun -ConfigDirectory $config } 'A nonfunctional DB_NAME override must be rejected.'
    Assert-True ($script:Calls.Count -eq 0) 'Invalid DB configuration must be rejected before Docker.'
    Write-Host "Quick-start PowerShell: $script:Checks checks passed (Docker mocked; no database access)."
} finally {
    [Environment]::SetEnvironmentVariable('DB_ADMIN_PW', $originalDbPassword, 'Process')
    Remove-Item Function:global:docker -ErrorAction SilentlyContinue
    Remove-Variable OpenMuDeploymentTestState -Scope Global -ErrorAction SilentlyContinue
    if ($originalDocker) { Set-Item Function:global:docker $originalDocker.ScriptBlock }
    if (Test-Path -LiteralPath $testRoot) {
        $resolvedRoot = [IO.Path]::GetFullPath($testRoot)
        if (-not $resolvedRoot.StartsWith($tempBase, [StringComparison]::OrdinalIgnoreCase) -or
            [IO.Path]::GetFileName($resolvedRoot) -notlike 'openmu-deploy-test-*') {
            throw "Refusing to clean an unexpected test path: $resolvedRoot"
        }
        Remove-Item -LiteralPath $resolvedRoot -Recurse -Force
    }
}
