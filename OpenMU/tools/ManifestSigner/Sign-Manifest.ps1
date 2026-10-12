<#
.SYNOPSIS
  Signs manifest.json with the offline RSA-2048 private key (detached PSS/SHA-256).

.DESCRIPTION
  Writes manifest.json.sig (base64 text of the RSA signature over the raw
  manifest.json bytes). The launcher verifies this signature with the public
  key embedded in ManifestSignatureVerifier.cs BEFORE any size/SHA-256 check.

  Private key format: MS-style <RSAKeyValue> XML (Modulus/Exponent/P/Q/DP/DQ/InverseQ/D).
  Works in Windows PowerShell 5.1 and PowerShell 7+ on Windows/macOS/Linux.

.EXAMPLE
  pwsh Sign-Manifest.ps1 -ManifestPath C:\OpenMU-Local\manifest.json `
      -PrivateKeyXmlPath D:\openmu自用\_安全审计-2026-10-12\keys\manifest-signing-private.xml
.EXAMPLE
  pwsh Sign-Manifest.ps1 -ManifestPath .\manifest.json -PrivateKeyXmlPath .\manifest-signing-public.xml -VerifyOnly
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ManifestPath,

    [string]$PrivateKeyXmlPath = $env:OPENMU_MANIFEST_SIGNING_KEY,

    [switch]$VerifyOnly
)

$ErrorActionPreference = 'Stop'

function Import-RsaParameters([string]$keyXmlPath) {
    [xml]$xml = Get-Content -LiteralPath $keyXmlPath -Raw
    $root = $xml.DocumentElement
    if ($root.LocalName -ne 'RSAKeyValue') {
        throw "Not an RSAKeyValue XML key: $keyXmlPath"
    }

    function Get-Bytes([string]$name) {
        $node = $root.SelectSingleNode($name)
        if (-not $node -or [string]::IsNullOrWhiteSpace($node.InnerText)) {
            return $null
        }

        return ,([Convert]::FromBase64String($node.InnerText.Trim()))
    }

    $params = [System.Security.Cryptography.RSAParameters]::new()
    $params.Modulus = (Get-Bytes 'Modulus')
    $params.Exponent = (Get-Bytes 'Exponent')
    $params.D = (Get-Bytes 'D')
    $params.P = (Get-Bytes 'P')
    $params.Q = (Get-Bytes 'Q')
    $params.DP = (Get-Bytes 'DP')
    $params.DQ = (Get-Bytes 'DQ')
    $params.InverseQ = (Get-Bytes 'InverseQ')
    if (-not $params.Modulus -or -not $params.Exponent) {
        throw "Incomplete key XML (Modulus/Exponent required): $keyXmlPath"
    }

    return $params
}

$manifestPath = [IO.Path]::GetFullPath($ManifestPath)
if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
    throw "manifest not found: $manifestPath"
}

if ([string]::IsNullOrWhiteSpace($PrivateKeyXmlPath)) {
    throw "No key XML provided. Pass -PrivateKeyXmlPath or set OPENMU_MANIFEST_SIGNING_KEY."
}

$keyPath = [IO.Path]::GetFullPath($PrivateKeyXmlPath)
if (-not (Test-Path -LiteralPath $keyPath -PathType Leaf)) {
    throw "key XML not found: $keyPath"
}

$manifestBytes = [IO.File]::ReadAllBytes($manifestPath)
$signaturePath = $manifestPath + '.sig'

# PSS requires RSACng on .NET Framework (PS 5.1); RSA.Create() on .NET Core already supports PSS.
if ($PSEdition -eq 'Core') {
    $rsa = [System.Security.Cryptography.RSA]::Create(2048)
}
else {
    $rsa = New-Object System.Security.Cryptography.RSACng(2048)
}
try {
    $rsa.ImportParameters((Import-RsaParameters $keyPath))
    if ($VerifyOnly) {
        if (-not (Test-Path -LiteralPath $signaturePath -PathType Leaf)) {
            throw "missing detached signature: $signaturePath"
        }

        $signature = [Convert]::FromBase64String([IO.File]::ReadAllText($signaturePath).Trim())
        if (-not $rsa.VerifyData($manifestBytes, $signature, [System.Security.Cryptography.HashAlgorithmName]::SHA256, [System.Security.Cryptography.RSASignaturePadding]::Pss)) {
            throw "signature INVALID: $manifestPath was not signed by the key in $keyPath"
        }

        Write-Output "signature VALID: $signaturePath"
        return
    }

    $signatureBytes = $rsa.SignData($manifestBytes, [System.Security.Cryptography.HashAlgorithmName]::SHA256, [System.Security.Cryptography.RSASignaturePadding]::Pss)
    [IO.File]::WriteAllText($signaturePath, [Convert]::ToBase64String($signatureBytes), (New-Object Text.ASCIIEncoding))
    Write-Output "signed: $signaturePath"
}
finally {
    $rsa.Dispose()
}