function Remove-PackagingDirectorySafely {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        return
    }

    $item = Get-Item -LiteralPath $Path -Force
    if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
        throw "Refusing to remove reparse-point packaging path: $Path"
    }

    Remove-Item -LiteralPath $Path -Recurse -Force
}

function Assert-GameConfigTemplate {
    param(
        [Parameter(Mandatory = $true)]
        [string]$TemplatePath
    )

    if (-not (Test-Path -LiteralPath $TemplatePath -PathType Leaf)) {
        throw "Game publish directory does not contain config.ini.template: $(Split-Path -Parent $TemplatePath)"
    }

    $requiredValues = [Collections.Generic.Dictionary[string,string]]::new([StringComparer]::OrdinalIgnoreCase)
    $requiredValues.Add('LOGIN.RememberMe', '0')
    $requiredValues.Add('LOGIN.EncryptedUsername', '')
    $requiredValues.Add('LOGIN.EncryptedPassword', '')
    $requiredValues.Add('UI.Locale', 'zh-CN')
    $actualValues = [Collections.Generic.Dictionary[string,string]]::new([StringComparer]::OrdinalIgnoreCase)
    $section = ''

    foreach ($line in Get-Content -LiteralPath $TemplatePath) {
        $trimmed = $line.Trim()
        if ($trimmed.Length -eq 0 -or $trimmed.StartsWith(';') -or $trimmed.StartsWith('#')) {
            continue
        }

        if ($trimmed -match '^\[(?<section>[^]]+)\]$') {
            $section = $Matches.section.Trim()
            continue
        }

        if ($trimmed -match '^(?<key>[^=]+)=(?<value>.*)$') {
            $qualifiedKey = "$section.$($Matches.key.Trim())"
            if ($requiredValues.ContainsKey($qualifiedKey)) {
                if ($actualValues.ContainsKey($qualifiedKey)) {
                    throw "Game config template contains duplicate security-sensitive key: $qualifiedKey"
                }

                $actualValues.Add($qualifiedKey, $Matches.value.Trim())
            }
        }
    }

    foreach ($entry in $requiredValues.GetEnumerator()) {
        if (-not $actualValues.ContainsKey($entry.Key) -or $actualValues[$entry.Key] -cne $entry.Value) {
            throw "Game config template must set $($entry.Key)=$($entry.Value)."
        }
    }
}

function Copy-GamePayload {
    param(
        [Parameter(Mandatory = $true)]
        [string]$SourceDirectory,

        [Parameter(Mandatory = $true)]
        [string]$DestinationDirectory
    )

    $source = [IO.Path]::GetFullPath($SourceDirectory)
    $destination = [IO.Path]::GetFullPath($DestinationDirectory)
    $template = Join-Path $source 'config.ini.template'

    if (-not (Test-Path -LiteralPath (Join-Path $source 'Main.exe') -PathType Leaf)) {
        throw "Game publish directory does not contain Main.exe: $source"
    }

    Assert-GameConfigTemplate -TemplatePath $template
    New-Item -ItemType Directory -Path $destination -Force | Out-Null

    Get-ChildItem -LiteralPath $source -Force |
        Where-Object { $_.Extension -ne '.lib' -and $_.Name -ne 'config.ini' } |
        Copy-Item -Destination $destination -Recurse -Force
    $packagedConfig = Join-Path $destination 'config.ini'
    Copy-Item -LiteralPath $template -Destination $packagedConfig -Force
    Assert-GameConfigTemplate -TemplatePath $packagedConfig
}
