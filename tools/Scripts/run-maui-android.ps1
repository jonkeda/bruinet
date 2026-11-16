#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Runs an app's MAUI UI tests on one Android emulator or device through Appium (UiAutomator2)
    or Bruinet's DevFlow runner.

.DESCRIPTION
    Builds a Debug APK with its assemblies embedded (Appium installs it itself, so Fast Deployment
    must be off) for the host's emulator ABI, builds the UI-test project for net10.0, uninstalls the
    app so Appium installs the new build, reverses the given ports into the device so the app reaches
    a mock server on the host at 127.0.0.1, and runs dotnet test with APPIUM_PLATFORM=android.
    For Bruinet's own sample suite use run-android-tests.ps1.

    With -Runner devflow the app is built with BruinetDevFlow=true (the DevFlow agent and Bruinet's
    bridge) and kept as bin/DevFlowAndroid/<PackageId>-Signed.apk next to the app project, so an
    Appium build does not overwrite it. The runner installs and starts that APK itself; no Appium
    server is needed, and the test project must reference Bruinet.Maui.DevFlow.

    Needs one booted device (an emulator with at least 3 GB RAM: emulator -avd <name> -memory 4096)
    and a running Appium server with the uiautomator2 driver:
    appium --address 127.0.0.1 --port 4723 --relaxed-security. APPIUM_SERVER_URI overrides the address.

.PARAMETER AppProject
    The MAUI app project (.csproj).

.PARAMETER TestProject
    The UI-test project (.csproj).

.PARAMETER PackageId
    The app's Android package id, to uninstall it before the run.

.PARAMETER ReversePort
    Host ports the app reaches on the device as 127.0.0.1:<port>, for example a mock backend.

.PARAMETER Filter
    dotnet test filter.

.PARAMETER NoBuild
    Skip both builds and the uninstall.

.PARAMETER Runner
    appium (default) or devflow.

.PARAMETER ResultsDirectory
    When set, writes a TRX report to this folder.

.PARAMETER RepositoryRoot
    The app repository's root, to check its Bruinet submodule. Skipped when empty.

.PARAMETER SubmodulePath
    The Bruinet submodule's path below -RepositoryRoot. Default: Brinell.

.EXAMPLE
    ./Brinell/tools/Scripts/run-maui-android.ps1 -AppProject MyApp/MyApp.csproj -TestProject MyApp.UITests/MyApp.UITests.csproj `
        -PackageId com.example.myapp -ReversePort 9090 -Filter "FullyQualifiedName~Smoke"
#>
param(
    [Parameter(Mandatory)][string]$AppProject,
    [Parameter(Mandatory)][string]$TestProject,
    [Parameter(Mandatory)][string]$PackageId,
    [int[]]$ReversePort = @(),
    [string]$Filter,
    [switch]$NoBuild,
    [string]$ResultsDirectory,
    [string]$RepositoryRoot,
    [string]$SubmodulePath = 'Brinell',
    [ValidateSet('appium', 'devflow')][string]$Runner = 'appium'
)

$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'RunGuards.psm1') -Force
if ($RepositoryRoot) { Test-BruinetSubmodule -RepositoryRoot $RepositoryRoot -Path $SubmodulePath | Out-Null }

$serverUri = if ($env:APPIUM_SERVER_URI) { $env:APPIUM_SERVER_URI } else { 'http://127.0.0.1:4723' }

$defaultSdk = if ($IsMacOS) { Join-Path $HOME 'Library/Android/sdk' } elseif ($IsLinux) { Join-Path $HOME 'Android/Sdk' } else { Join-Path $env:LOCALAPPDATA 'Android/Sdk' }
$sdk = if ($env:ANDROID_HOME) { $env:ANDROID_HOME } elseif ($env:ANDROID_SDK_ROOT) { $env:ANDROID_SDK_ROOT } else { $defaultSdk }
$adb = Join-Path $sdk ($IsWindows ? 'platform-tools/adb.exe' : 'platform-tools/adb')
if (-not (Test-Path $adb)) { throw "adb not found at '$adb'. Set ANDROID_HOME." }
# The tests and Bruinet start adb themselves and find it through ANDROID_HOME.
$env:ANDROID_HOME = $sdk

$devFlow = $Runner -eq 'devflow'
$devFlowApk = Join-Path (Split-Path -Parent (Resolve-Path $AppProject)) "bin/DevFlowAndroid/$PackageId-Signed.apk"

if (-not $devFlow) {
    try {
        Invoke-WebRequest "$serverUri/status" -UseBasicParsing -TimeoutSec 5 | Out-Null
    }
    catch {
        throw "Appium is not reachable at $serverUri. Start it with: appium --address 127.0.0.1 --port 4723 --relaxed-security"
    }
}

$devices = @(& $adb devices | Select-String '\tdevice$')
if ($devices.Count -ne 1) {
    throw "Expected exactly one booted Android device, found $($devices.Count)."
}

if (-not $NoBuild) {
    # One ABI keeps the APK small enough to install; an Apple-silicon host runs an arm64 emulator image.
    $rid = [System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture -eq 'Arm64' ? 'android-arm64' : 'android-x64'
    $buildArgs = @($AppProject, '-f', 'net10.0-android', '-c', 'Debug', '-r', $rid, '-p:EmbedAssembliesIntoApk=true')
    if ($devFlow) { $buildArgs += '-p:BruinetDevFlow=true' }
    dotnet build @buildArgs
    if ($LASTEXITCODE -ne 0) { throw 'Android app build failed.' }

    if ($devFlow) {
        $built = Get-ChildItem (Join-Path (Split-Path -Parent (Resolve-Path $AppProject)) "bin/Debug/net10.0-android/$rid") -Filter "$PackageId-Signed.apk" |
            Select-Object -First 1
        if (-not $built) { throw "No $PackageId-Signed.apk after the DevFlow build." }
        New-Item -ItemType Directory -Force (Split-Path -Parent $devFlowApk) | Out-Null
        Copy-Item $built.FullName $devFlowApk -Force
    }

    dotnet build $TestProject -f net10.0 -p:TargetFrameworks=net10.0
    if ($LASTEXITCODE -ne 0) { throw 'UI-test build failed.' }

    # Appium skips installing an APK whose version code is already on the device; the DevFlow runner replaces it.
    if (-not $devFlow) { & $adb uninstall $PackageId 2>$null | Out-Null }
}

foreach ($port in $ReversePort) {
    & $adb reverse "tcp:$port" "tcp:$port" | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "adb reverse for port $port failed." }
}

$env:APPIUM_PLATFORM = 'android'
if ($devFlow) {
    if (-not (Test-Path $devFlowApk)) { throw "No DevFlow build at '$devFlowApk'. Run once without -NoBuild." }
    $env:BRUINET_MAUI_RUNNER = 'devflow'
    if (-not $env:APPIUM_APP_PATH) { $env:APPIUM_APP_PATH = $devFlowApk }
}

$testArgs = @($TestProject, '-f', 'net10.0', '-p:TargetFrameworks=net10.0', '--no-build')
if ($Filter) { $testArgs += @('--filter', $Filter) }
if ($ResultsDirectory) { $testArgs += @('--logger', 'trx', '--results-directory', $ResultsDirectory) }

dotnet test @testArgs
exit $LASTEXITCODE
