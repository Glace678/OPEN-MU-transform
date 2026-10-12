// <copyright file="PackageManifestValidatorTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.LocalLauncher.Tests;

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

/// <summary>
/// Tests the portable-package trust boundary.
/// </summary>
public class PackageManifestValidatorTests
{
    private string _directory = null!;
    private string? _previousAllowUnsigned;

    /// <summary>Creates an isolated package directory.</summary>
    [SetUp]
    public void SetUp()
    {
        this._directory = Path.Combine(Path.GetTempPath(), $"openmu-manifest-{Guid.NewGuid():N}");
        Directory.CreateDirectory(this._directory);

        // These tests exercise the size/SHA-256/required-file logic that runs AFTER the
        // signature gate, so tolerate a missing signature via the explicit dev switch.
        // The signature fail-closed behavior is covered by the dedicated tests below.
        this._previousAllowUnsigned = Environment.GetEnvironmentVariable(ManifestSignatureVerifier.AllowUnsignedEnvironmentVariableName);
        Environment.SetEnvironmentVariable(ManifestSignatureVerifier.AllowUnsignedEnvironmentVariableName, "1");
    }

    /// <summary>Removes the isolated package directory.</summary>
    [TearDown]
    public void TearDown()
    {
        Environment.SetEnvironmentVariable(ManifestSignatureVerifier.AllowUnsignedEnvironmentVariableName, this._previousAllowUnsigned);
        Directory.Delete(this._directory, recursive: true);
    }

    /// <summary>Verifies paths cannot escape the portable package root.</summary>
    [Test]
    public void TraversalPathIsRejected()
    {
        var manifest = new
        {
            formatVersion = 1,
            version = "0.9.10-local.1",
            files = new[] { new { path = "../outside.exe", size = 0, sha256 = new string('0', 64) } },
        };
        File.WriteAllBytes(Path.Combine(this._directory, "manifest.json"), JsonSerializer.SerializeToUtf8Bytes(manifest));

        Assert.ThrowsAsync<InvalidDataException>(async () =>
            await new PackageManifestValidator().ValidateAsync(new LocalPaths(this._directory), CancellationToken.None));
    }

    /// <summary>Verifies a truncated manifest cannot omit required package executables.</summary>
    [Test]
    public void MissingRequiredFilesAreRejected()
    {
        var filePath = Path.Combine(this._directory, "placeholder.txt");
        File.WriteAllText(filePath, string.Empty);
        var manifest = new
        {
            formatVersion = 1,
            version = "0.9.10-local.1",
            files = new[] { new { path = "placeholder.txt", size = 0, sha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Array.Empty<byte>())) } },
        };
        File.WriteAllBytes(Path.Combine(this._directory, "manifest.json"), JsonSerializer.SerializeToUtf8Bytes(manifest));

        Assert.ThrowsAsync<InvalidDataException>(async () =>
            await new PackageManifestValidator().ValidateAsync(new LocalPaths(this._directory), CancellationToken.None));
    }

    /// <summary>The Windows client cannot log in without its native protocol library.</summary>
    [Test]
    [Platform("Win")]
    public async Task ProtocolLibraryMustBeIncludedInTheManifest()
    {
        var requiredPaths = new[]
        {
            "OpenMU-Local.exe", "README-简体中文.txt", "App/Server/MUnique.OpenMU.Startup.exe",
            "App/Game/Main.exe", "App/Game/MUnique.Client.Library.dll", "App/Game/config.ini.template",
            "Runtime/PostgreSQL/bin/initdb.exe", "Runtime/PostgreSQL/bin/pg_ctl.exe",
            "Runtime/PostgreSQL/bin/pg_isready.exe", "Runtime/PostgreSQL/bin/pg_dump.exe",
            "Runtime/PostgreSQL/bin/pg_restore.exe", "Runtime/PostgreSQL/bin/vcruntime140.dll",
            "App/Game/msvcp140.dll", "App/Game/vcruntime140.dll", "App/Game/vcruntime140_1.dll",
            "Licenses/OpenMU-MIT.txt", "Licenses/Microsoft-Visual-Cpp-Redistributables.txt",
            "Licenses/PostgreSQL.txt", "Licenses/PostgreSQL-ThirdParty.txt",
        };
        var hash = Convert.ToHexString(SHA256.HashData(Array.Empty<byte>()));
        var entries = requiredPaths.Select(path => new { path, size = 0, sha256 = hash }).ToArray();
        foreach (var path in requiredPaths)
        {
            var fullPath = Path.Combine(this._directory, path);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.WriteAllBytes(fullPath, []);
        }

        var manifestPath = Path.Combine(this._directory, "manifest.json");
        File.WriteAllBytes(manifestPath, JsonSerializer.SerializeToUtf8Bytes(new { formatVersion = 1, version = "0.9.10-local.1", files = entries }));
        await new PackageManifestValidator().ValidateAsync(new LocalPaths(this._directory), CancellationToken.None);

        File.WriteAllBytes(manifestPath, JsonSerializer.SerializeToUtf8Bytes(new
        {
            formatVersion = 1,
            version = "0.9.10-local.1",
            files = entries.Where(entry => entry.path != "App/Game/MUnique.Client.Library.dll"),
        }));

        var exception = Assert.ThrowsAsync<InvalidDataException>(async () =>
            await new PackageManifestValidator().ValidateAsync(new LocalPaths(this._directory), CancellationToken.None));
        Assert.That(exception!.Message, Does.Contain("App/Game/MUnique.Client.Library.dll"));
    }

    /// <summary>A malformed manifest reports invalid data without dereferencing a null entry.</summary>
    [Test]
    public void NullManifestEntriesAreRejected()
    {
        File.WriteAllText(Path.Combine(this._directory, "manifest.json"),
            "{\"formatVersion\":1,\"version\":\"0.9.10-local.1\",\"files\":[null]}");

        Assert.ThrowsAsync<InvalidDataException>(async () =>
            await new PackageManifestValidator().ValidateAsync(new LocalPaths(this._directory), CancellationToken.None));
    }

    /// <summary>Verifies legacy manifests cannot reject player-written game settings.</summary>
    [Test]
    public void MutableGameConfigurationIsNotContentValidated()
    {
        var gameDirectory = Path.Combine(this._directory, "App", "Game");
        Directory.CreateDirectory(gameDirectory);
        File.WriteAllText(Path.Combine(gameDirectory, "config.ini"), "[Window]\nWindowed=1\n");
        var manifest = new
        {
            formatVersion = 1,
            version = "0.9.10-local.1",
            files = new[]
            {
                new
                {
                    path = "App/Game/config.ini",
                    size = 0,
                    sha256 = Convert.ToHexString(SHA256.HashData(Array.Empty<byte>())),
                },
            },
        };
        File.WriteAllBytes(Path.Combine(this._directory, "manifest.json"), JsonSerializer.SerializeToUtf8Bytes(manifest));

        var exception = Assert.ThrowsAsync<InvalidDataException>(async () =>
            await new PackageManifestValidator().ValidateAsync(new LocalPaths(this._directory), CancellationToken.None));

        Assert.That(exception!.Message, Does.StartWith("便携程序包清单未包含必需文件"));
    }

    // --- Signature trust-anchor tests (F5, W2-01) --------------------------------

    /// <summary>An unsigned manifest is rejected by default (no dev switch), fail-closed.</summary>
    [Test]
    public void MissingSignatureIsRejectedByDefault()
    {
        Environment.SetEnvironmentVariable(ManifestSignatureVerifier.AllowUnsignedEnvironmentVariableName, null);
        File.WriteAllBytes(Path.Combine(this._directory, "manifest.json"), "{}"u8.ToArray());

        var exception = Assert.ThrowsAsync<InvalidDataException>(async () =>
            await new PackageManifestValidator().ValidateAsync(new LocalPaths(this._directory), CancellationToken.None));
        StringAssert.Contains("manifest.json.sig", exception!.Message);
    }

    /// <summary>A present-but-invalid signature is always rejected, even with the dev switch set.</summary>
    [Test]
    public void InvalidSignatureIsRejectedEvenWhenMissingWouldBeAllowed()
    {
        // The dev switch only tolerates a MISSING signature file; a present .sig that does
        // not verify must still fail closed.
        File.WriteAllBytes(Path.Combine(this._directory, "manifest.json"), "{}"u8.ToArray());
        File.WriteAllText(Path.Combine(this._directory, "manifest.json.sig"), Convert.ToBase64String(new byte[256]));

        var exception = Assert.ThrowsAsync<InvalidDataException>(async () =>
            await new PackageManifestValidator().ValidateAsync(new LocalPaths(this._directory), CancellationToken.None));
        StringAssert.Contains("签名校验失败", exception!.Message);
    }

    /// <summary>The verifier rejects a signature whose bytes are not valid base64.</summary>
    [Test]
    public void GarbageSignatureContentIsRejected()
    {
        var manifestBytes = "{}"u8.ToArray();
        Assert.That(ManifestSignatureVerifier.TryVerify(manifestBytes, Encoding.ASCII.GetBytes("not-base64!!")), Is.False);
    }
}
