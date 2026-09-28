[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateSet('Initialize', 'Tap', 'Set', 'Neutral', 'Disconnect', 'ReadRumble')]
    [string]$Action,

    [Parameter(Mandatory)]
    [string]$StatePath,

    [ValidateSet(
        'LeftX', 'LeftY', 'RightX', 'RightY', 'LeftTrigger', 'RightTrigger',
        'South', 'East', 'West', 'North', 'Back', 'Guide', 'Start',
        'LeftStick', 'RightStick', 'LeftShoulder', 'RightShoulder',
        'DpadUp', 'DpadDown', 'DpadLeft', 'DpadRight')]
    [string]$Control,

    [ValidateRange(-32768, 32767)]
    [int]$Value = 0,

    [ValidateRange(16, 5000)]
    [int]$HoldMilliseconds = 100,

    [ValidateRange(0, 5000)]
    [int]$ObserveRumbleMilliseconds = 0
)

$ErrorActionPreference = 'Stop'

if (-not [IO.Path]::IsPathFullyQualified($StatePath)) {
    throw 'StatePath must be an absolute path.'
}

$StatePath = [IO.Path]::GetFullPath($StatePath)
$stateDirectory = Split-Path -Parent $StatePath
if (-not (Test-Path -LiteralPath $stateDirectory -PathType Container)) {
    New-Item -ItemType Directory -Path $stateDirectory | Out-Null
}

$iniType = 'MuMain.ControllerAcceptance.IniFile' -as [type]
if (-not $iniType) {
    $iniType = Add-Type -PassThru -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
using System.Text;

namespace MuMain.ControllerAcceptance
{
    public static class IniFile
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool WritePrivateProfileString(
            string section,
            string key,
            string value,
            string fileName);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        public static extern uint GetPrivateProfileString(
            string section,
            string key,
            string defaultValue,
            StringBuilder returnedString,
            uint size,
            string fileName);
    }
}
'@
    $iniType = $iniType | Where-Object FullName -eq 'MuMain.ControllerAcceptance.IniFile'
}

$axisDefaults = [ordered]@{
    LeftX = 0
    LeftY = 0
    RightX = 0
    RightY = 0
    LeftTrigger = -32768
    RightTrigger = -32768
}

$buttonNames = @(
    'South', 'East', 'West', 'North', 'Back', 'Guide', 'Start',
    'LeftStick', 'RightStick', 'LeftShoulder', 'RightShoulder',
    'DpadUp', 'DpadDown', 'DpadLeft', 'DpadRight'
)

function Set-IniValue([string]$Section, [string]$Key, [string]$IniValue) {
    if (-not $iniType::WritePrivateProfileString(
        $Section,
        $Key,
        $IniValue,
        $StatePath)) {
        $errorCode = [Runtime.InteropServices.Marshal]::GetLastWin32Error()
        throw "Failed to write [$Section] $Key to '$StatePath' (Win32 $errorCode)."
    }
}

function Get-IniValue([string]$Section, [string]$Key, [string]$DefaultValue = '0') {
    $buffer = [Text.StringBuilder]::new(256)
    [void]$iniType::GetPrivateProfileString(
        $Section,
        $Key,
        $DefaultValue,
        $buffer,
        [uint32]$buffer.Capacity,
        $StatePath)
    return $buffer.ToString()
}

function Set-NeutralState {
    Set-IniValue 'Gamepad' 'Connected' '1'
    foreach ($entry in $axisDefaults.GetEnumerator()) {
        Set-IniValue 'Gamepad' $entry.Key ([string]$entry.Value)
    }

    foreach ($button in $buttonNames) {
        Set-IniValue 'Gamepad' $button '0'
    }
}

function Read-RumbleState {
    return [pscustomobject]@{
        Sequence = [int](Get-IniValue 'Rumble' 'Sequence')
        Low = [int](Get-IniValue 'Rumble' 'Low')
        High = [int](Get-IniValue 'Rumble' 'High')
        DurationMs = [int](Get-IniValue 'Rumble' 'DurationMs')
    }
}

function Add-RumbleSample(
    [Collections.Generic.List[object]]$Samples,
    [Diagnostics.Stopwatch]$Stopwatch,
    [ref]$PreviousSequence) {
    $state = Read-RumbleState
    if ($state.Sequence -ne $PreviousSequence.Value) {
        $Samples.Add([pscustomobject]@{
            AtMs = [math]::Round($Stopwatch.Elapsed.TotalMilliseconds, 1)
            Sequence = $state.Sequence
            Low = $state.Low
            High = $state.High
            DurationMs = $state.DurationMs
        })
        $PreviousSequence.Value = $state.Sequence
    }
}

switch ($Action) {
    'Initialize' {
        Set-NeutralState
        Set-IniValue 'Rumble' 'Low' '0'
        Set-IniValue 'Rumble' 'High' '0'
        Set-IniValue 'Rumble' 'DurationMs' '0'
        Set-IniValue 'Rumble' 'Sequence' '0'
        [pscustomobject]@{ StatePath = $StatePath; Connected = $true; Neutral = $true } |
            ConvertTo-Json -Compress
    }
    'Neutral' {
        Set-NeutralState
    }
    'Disconnect' {
        Set-IniValue 'Gamepad' 'Connected' '0'
    }
    'Set' {
        if ([string]::IsNullOrWhiteSpace($Control)) {
            throw 'Control is required for Set.'
        }

        if ($buttonNames -contains $Control -and $Value -notin 0, 1) {
            throw 'Button values must be 0 or 1.'
        }

        Set-IniValue 'Gamepad' $Control ([string]$Value)
    }
    'ReadRumble' {
        Read-RumbleState | ConvertTo-Json -Compress
    }
    'Tap' {
        if ($buttonNames -notcontains $Control) {
            throw 'Tap requires a button control.'
        }

        $samples = [Collections.Generic.List[object]]::new()
        $previousSequence = (Read-RumbleState).Sequence
        $stopwatch = [Diagnostics.Stopwatch]::StartNew()
        Set-IniValue 'Gamepad' $Control '1'
        while ($stopwatch.ElapsedMilliseconds -lt $HoldMilliseconds) {
            Add-RumbleSample $samples $stopwatch ([ref]$previousSequence)
            Start-Sleep -Milliseconds 5
        }

        Set-IniValue 'Gamepad' $Control '0'
        $observationEnd = $HoldMilliseconds + $ObserveRumbleMilliseconds
        while ($stopwatch.ElapsedMilliseconds -lt $observationEnd) {
            Add-RumbleSample $samples $stopwatch ([ref]$previousSequence)
            Start-Sleep -Milliseconds 5
        }

        Add-RumbleSample $samples $stopwatch ([ref]$previousSequence)
        [pscustomobject]@{
            Control = $Control
            HoldMilliseconds = $HoldMilliseconds
            ObservedMilliseconds = [math]::Round($stopwatch.Elapsed.TotalMilliseconds, 1)
            RumbleChanges = $samples
        } | ConvertTo-Json -Depth 5
    }
}
