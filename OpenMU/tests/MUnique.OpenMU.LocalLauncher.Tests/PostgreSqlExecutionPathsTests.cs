// <copyright file="PostgreSqlExecutionPathsTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.LocalLauncher.Tests;

/// <summary>
/// Tests the protected ASCII path boundary used by PostgreSQL on Windows.
/// </summary>
public class PostgreSqlExecutionPathsTests
{
    private string _testRoot = null!;
    private string _packageRoot = null!;
    private string _aliasBaseDirectory = null!;
    private PostgreSqlExecutionPaths? _executionPaths;

    /// <summary>Creates an isolated package under a non-ASCII path.</summary>
    [SetUp]
    public void SetUp()
    {
        this._testRoot = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"postgres-path-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(this._testRoot);
        this._packageRoot = Path.Combine(this._testRoot, "OpenMU-中文路径");
        this._aliasBaseDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "OpenMU-Local",
            "TestAliases",
            Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(this._aliasBaseDirectory);
        }
        catch (UnauthorizedAccessException)
        {
            Assert.Ignore("当前测试宿主禁止写入 ProgramData；请在非沙箱 Windows 会话运行 junction 集成测试。");
        }

        var paths = new LocalPaths(this._packageRoot);
        Directory.CreateDirectory(paths.PostgreSqlDataDirectory);
        Directory.CreateDirectory(paths.KeysDirectory);
        Directory.CreateDirectory(paths.LogsDirectory);
        Directory.CreateDirectory(paths.BackupsDirectory);
        Directory.CreateDirectory(paths.PostgreSqlRuntimeDirectory);
    }

    /// <summary>Removes aliases without following their junction targets.</summary>
    [TearDown]
    public void TearDown()
    {
        if (this._executionPaths is not null && Directory.Exists(this._executionPaths.RootDirectory))
        {
            foreach (var junction in new[]
                     {
                         this._executionPaths.RuntimeDirectory,
                         this._executionPaths.DataDirectory,
                         this._executionPaths.KeysDirectory,
                         this._executionPaths.LogsDirectory,
                         this._executionPaths.BackupsDirectory,
                     })
            {
                if (Directory.Exists(junction) && (File.GetAttributes(junction) & FileAttributes.ReparsePoint) != 0)
                {
                    Directory.Delete(junction);
                }
            }
        }

        if (Directory.Exists(this._aliasBaseDirectory))
        {
            Directory.Delete(this._aliasBaseDirectory, recursive: true);
        }

        if (Directory.Exists(this._testRoot))
        {
            Directory.Delete(this._testRoot, recursive: true);
        }
    }

    /// <summary>Verifies every path passed to PostgreSQL is ASCII while data stays in the package.</summary>
    [Test]
    public void UnicodePackageGetsAsciiExecutionPathsWithPersistentPackageData()
    {
        var paths = new LocalPaths(this._packageRoot);
        this._executionPaths = PostgreSqlExecutionPaths.Create(paths, this._aliasBaseDirectory);

        var commandPaths = new[]
        {
            this._executionPaths.RootDirectory,
            this._executionPaths.RuntimeDirectory,
            this._executionPaths.DataDirectory,
            this._executionPaths.KeysDirectory,
            this._executionPaths.LogsDirectory,
            this._executionPaths.BackupsDirectory,
            this._executionPaths.InitDbExecutable,
            this._executionPaths.PgCtlExecutable,
            this._executionPaths.PgIsReadyExecutable,
            this._executionPaths.PgDumpExecutable,
            this._executionPaths.PgRestoreExecutable,
        };
        Assert.That(commandPaths, Has.All.Matches<string>(path => path.All(character => character <= 0x7F)));

        File.WriteAllText(Path.Combine(this._executionPaths.DataDirectory, "alias-write.txt"), "persistent");
        Assert.That(File.ReadAllText(Path.Combine(paths.PostgreSqlDataDirectory, "alias-write.txt")), Is.EqualTo("persistent"));
    }

    /// <summary>Verifies repeated initialization validates and reuses the same junctions.</summary>
    [Test]
    public void ExistingValidAliasesAreReused()
    {
        var paths = new LocalPaths(this._packageRoot);
        this._executionPaths = PostgreSqlExecutionPaths.Create(paths, this._aliasBaseDirectory);
        var repeated = PostgreSqlExecutionPaths.Create(paths, this._aliasBaseDirectory);

        Assert.That(repeated.RootDirectory, Is.EqualTo(this._executionPaths.RootDirectory));
        Assert.DoesNotThrow(repeated.Validate);
    }

    /// <summary>Verifies a replaced alias is rejected instead of silently trusted.</summary>
    [Test]
    public void ReplacedAliasIsRejected()
    {
        var paths = new LocalPaths(this._packageRoot);
        this._executionPaths = PostgreSqlExecutionPaths.Create(paths, this._aliasBaseDirectory);
        Directory.Delete(this._executionPaths.RuntimeDirectory);
        Directory.CreateDirectory(this._executionPaths.RuntimeDirectory);

        Assert.Throws<InvalidDataException>(this._executionPaths.Validate);
    }

}
