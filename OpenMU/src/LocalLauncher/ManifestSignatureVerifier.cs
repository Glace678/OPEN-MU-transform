// <copyright file="ManifestSignatureVerifier.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.LocalLauncher;

using System.Security.Cryptography;
using System.Text;

/// <summary>
/// Verifies the detached RSA-2048 / PSS-SHA-256 signature of <c>manifest.json</c>.
/// The public key below is the compile-time trust anchor (F5, 2026-10-12);
/// the matching private key is kept offline and never ships with any package.
/// </summary>
public static class ManifestSignatureVerifier
{
    /// <summary>Base64 of the embedded RSA-2048 public modulus.</summary>
    private const string PublicModulusBase64 =
        "0NDNjBqjwEnisrs5gJDzZrXN7MkKR3jg9SXPMagifZ7BalOdlBQk/KjLSJw0VUXMvAxYvAjbsCOBSO0pl38GaUCNfcGyBUfJ429be8pDM/uWt2J2IOTd5F5HufMQIlxxkDS5SukQcyJNZmKzch4/GOqOV29J+rEUg36Ih0XnM3gZpwZv8HwTC2xLwmn61D6JfoOvYwEPX57B1hPndMljpwSXpIGtqNfRL4VvE6PimGSRMQN5fckLAEpHXi/IwsQqQ8sm3JAN6Jth7LV7KADJyfA+iTKXdQInZwIvwOkmF1c5NLaBGVzCdhAMuawn3FMZLIDL//pd5bSLmDgAPBpmRQ==";

    /// <summary>Base64 of the embedded RSA public exponent.</summary>
    private const string PublicExponentBase64 = "AQAB";

    /// <summary>
    /// Development-only environment switch: set to "1" to tolerate a missing manifest
    /// signature (with a warning). A present-but-invalid signature is always rejected.
    /// </summary>
    public const string AllowUnsignedEnvironmentVariableName = "OPENMU_ALLOW_UNSIGNED_MANIFEST";

    /// <summary>Gets the detached signature file path for a manifest file.</summary>
    public static string SignatureFilePath(string manifestPath) => manifestPath + ".sig";

    /// <summary>
    /// Verifies the detached signature over the raw manifest bytes.
    /// </summary>
    /// <param name="manifestBytes">The raw bytes of manifest.json (as signed).</param>
    /// <param name="signatureFileBytes">The raw bytes of manifest.json.sig (base64 text of the RSA signature).</param>
    /// <returns><c>true</c> when the signature was produced by the embedded trust anchor.</returns>
    public static bool TryVerify(byte[] manifestBytes, byte[] signatureFileBytes)
    {
        if (manifestBytes is not { Length: > 0 } || signatureFileBytes is not { Length: > 0 })
        {
            return false;
        }

        try
        {
            var signature = Convert.FromBase64String(Encoding.ASCII.GetString(signatureFileBytes).Trim());
            using var rsa = RSA.Create();
            rsa.ImportParameters(new RSAParameters
            {
                Modulus = Convert.FromBase64String(PublicModulusBase64),
                Exponent = Convert.FromBase64String(PublicExponentBase64),
            });
            return rsa.VerifyData(manifestBytes, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
        }
        catch (FormatException)
        {
            return false;
        }
        catch (CryptographicException)
        {
            return false;
        }
    }

    /// <summary>
    /// Fails closed unless the manifest is covered by a valid detached signature.
    /// Must run before any size/SHA-256 comparison of packaged files.
    /// </summary>
    /// <param name="manifestPath">Path to manifest.json.</param>
    /// <param name="manifestBytes">The already-read raw bytes of manifest.json.</param>
    public static void EnsureVerified(string manifestPath, byte[] manifestBytes)
    {
        var signaturePath = manifestPath + ".sig";
        if (!File.Exists(signaturePath))
        {
            if (string.Equals(
                    Environment.GetEnvironmentVariable(AllowUnsignedEnvironmentVariableName),
                    "1",
                    StringComparison.Ordinal))
            {
                Console.Error.WriteLine(
                    "警告：manifest.json 缺少 detached 签名；因开发开关 OPENMU_ALLOW_UNSIGNED_MANIFEST=1 临时放行。正式分发包必须签名。");
                return;
            }

            throw new InvalidDataException(
                "程序包清单缺少签名文件 manifest.json.sig。分发构建必须携带清单签名；启动已拒绝（fail-closed）。");
        }

        if (!TryVerify(manifestBytes, File.ReadAllBytes(signaturePath)))
        {
            throw new InvalidDataException(
                "程序包清单签名校验失败：manifest.json 与 manifest.json.sig 不匹配，或签名非内置信任锚签发。启动已拒绝。");
        }
    }
}