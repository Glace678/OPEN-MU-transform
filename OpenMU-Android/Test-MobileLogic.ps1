[CmdletBinding()]
param([switch]$PortalOnly, [switch]$ResourcesOnly)

$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$mobileKey = 'A' * 43
$loginSeedArgument = '-POPENMU_MOBILE_LOGIN_SEED={0}' -f $mobileKey
$gmTokenArgument = '-POPENMU_MOBILE_GM_TOKEN={0}' -f $mobileKey
$serverArgument = '-POPENMU_SERVER_ADDRESS=127.0.0.1'
$gradleWrapper = if ($env:OS -eq 'Windows_NT') { '.\gradlew.bat' } else { './gradlew' }

. (Join-Path $projectRoot 'MobileResourceValidation.ps1')

function Test-MobileResources {
    $resources = Join-Path $projectRoot 'game-app/src/main/res'
    $reference = Read-MobileResourceCatalog (Join-Path $resources 'values')
    $locales = @('en', 'zh-CN', 'zh-TW', 'ja', 'ko', 'de', 'es', 'fr', 'pt', 'ru', 'uk', 'pl', 'id', 'vi', 'tl', 'fil')
    $directories = @('values') + @($locales | ForEach-Object { Get-MobileLocaleFolder $_ })
    foreach ($directory in $directories) {
        $catalog = Read-MobileResourceCatalog (Join-Path $resources $directory)
        Test-MobileResourceCatalog $reference $catalog $directory
        if ($catalog['app_name'].InnerText -cne 'OpenMU Mobile') {
            throw "Unexpected app brand in $directory"
        }
    }
    Write-Host 'Mobile resources: default + 15 cultures + Filipino alias, 884 strings and 34 arrays validated.'
}

function Get-MobileJUnitRuntime {
    $gradleCache = if ($env:GRADLE_USER_HOME) { $env:GRADLE_USER_HOME } else {
        Join-Path ([Environment]::GetFolderPath('UserProfile')) '.gradle'
    }
    $dependencyCache = Join-Path $gradleCache 'caches/modules-2/files-2.1'
    $junit = Get-ChildItem (Join-Path $dependencyCache 'junit/junit/4.13.2') `
        -Recurse -Filter 'junit-4.13.2.jar' | Select-Object -First 1 -ExpandProperty FullName
    $hamcrest = Get-ChildItem (Join-Path $dependencyCache 'org.hamcrest/hamcrest-core/1.3') `
        -Recurse -Filter 'hamcrest-core-1.3.jar' | Select-Object -First 1 -ExpandProperty FullName
    if (!$junit -or !$hamcrest) {
        throw 'JUnit runtime is unavailable; run the complete mobile logic test script to resolve it with Gradle'
    }
    return @($junit, $hamcrest)
}

function Invoke-PortalPolicyTests {
    $libraries = @(Get-MobileJUnitRuntime)
    $temporaryRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
    $output = Join-Path $temporaryRoot ('openmu-portal-tests-' + [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $output | Out-Null
    try {
        $sourceRoot = Join-Path $projectRoot 'game-app/src/main/java/net/munique/openmu/game'
        $sources = @('AccountPortalPolicy.java', 'LocalIpv4Address.java', 'MobileConnectionPolicy.java',
            'MobileIdentity.java') | ForEach-Object { Join-Path $sourceRoot $_ }
        $sources += Join-Path $projectRoot 'game-app/src/test/java/net/munique/openmu/game/AccountPortalPolicyTest.java'
        $classpath = $libraries -join [IO.Path]::PathSeparator
        & javac -encoding UTF-8 -cp $classpath -d $output @sources
        if ($LASTEXITCODE -ne 0) { throw 'Account portal policy compilation failed' }
        $runtimeClasspath = (@($output) + $libraries) -join [IO.Path]::PathSeparator
        & java -cp $runtimeClasspath org.junit.runner.JUnitCore net.munique.openmu.game.AccountPortalPolicyTest
        if ($LASTEXITCODE -ne 0) { throw 'Account portal policy tests failed' }
    } finally {
        $resolvedOutput = [IO.Path]::GetFullPath($output)
        if ([IO.Path]::GetDirectoryName($resolvedOutput) -ne $temporaryRoot.TrimEnd('\', '/') `
            -or [IO.Path]::GetFileName($resolvedOutput) -notlike 'openmu-portal-tests-*') {
            throw 'Refusing to clean up an unexpected portal test directory'
        }
        Remove-Item -LiteralPath $resolvedOutput -Recurse -Force
    }
}

Test-MobileResources
& (Join-Path $projectRoot 'Test-MobileResourceValidation.ps1')
if ($ResourcesOnly) {
    return
}
if ($PortalOnly) {
    Invoke-PortalPolicyTests
    return
}

function Invoke-MobileJUnitTests {
    param([string]$Module, [string[]]$RuntimeLibraries)

    $moduleRoot = Join-Path $projectRoot $Module
    $testClasses = Join-Path $moduleRoot 'build/intermediates/javac/debugUnitTest/classes'
    $mainClasses = Join-Path $moduleRoot 'build/intermediates/javac/debug/classes'
    $classpath = (@($testClasses, $mainClasses) + $RuntimeLibraries) -join [IO.Path]::PathSeparator
    $classes = @(Get-ChildItem -LiteralPath $testClasses -Recurse -Filter '*Test.class' | ForEach-Object {
        $_.FullName.Substring($testClasses.Length + 1).Replace('\', '.').Replace('/', '.') -replace '\.class$', ''
    })
    if ($classes.Count -eq 0) {
        throw "No mobile logic tests found for $Module"
    }

    # Gradle 8.8's Windows test worker cannot load classes from this Unicode
    # workspace with the installed JDK. Invoke JUnit directly with the compiled
    # classpath while still letting Gradle resolve and compile all test inputs.
    & java -cp $classpath org.junit.runner.JUnitCore @classes
    if ($LASTEXITCODE -ne 0) {
        throw "$Module mobile logic tests failed with exit code $LASTEXITCODE"
    }
}

Push-Location $projectRoot
try {
    & $gradleWrapper :game-app:compileDebugUnitTestJavaWithJavac :gm-app:compileDebugUnitTestJavaWithJavac $loginSeedArgument $gmTokenArgument $serverArgument
    if ($LASTEXITCODE -ne 0) {
        throw "Test compilation failed with exit code $LASTEXITCODE"
    }

    $libraries = @(Get-MobileJUnitRuntime)

    foreach ($module in @('game-app', 'gm-app')) {
        Invoke-MobileJUnitTests -Module $module -RuntimeLibraries $libraries
    }
} finally {
    Pop-Location
}
