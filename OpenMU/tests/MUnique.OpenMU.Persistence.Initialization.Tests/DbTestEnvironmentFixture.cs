// <copyright file="DbTestEnvironmentFixture.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Tests;

/// <summary>
/// Assembly-level setup fixture. The production
/// <see cref="EntityFramework.ConfigFileDatabaseConnectionStringProvider"/> fails fast when a
/// per-role database password environment variable is unset (ConnectionSettings.xml no longer
/// stores plaintext passwords). Several EF-only tests only inspect the model, migrations or
/// generated SQL and never open a real connection, but constructing a <c>DbContext</c> still
/// triggers <c>LoadSettings()</c>/<c>ApplyEnvironmentVariables()</c>. We inject non-secret
/// placeholder passwords for the whole process so those contexts can be built. This does NOT
/// weaken the production fail-fast contract: the real provider still throws for missing
/// variables in production; only the test process gets placeholders, which are restored after
/// the run.
/// </summary>
[SetUpFixture]
internal class DbTestEnvironmentFixture
{
    private static readonly string[] RequiredVariables =
    {
        "OPENMU_DB_ADMIN_PASSWORD",
        "OPENMU_DB_CONFIG_PASSWORD",
        "OPENMU_DB_ACCOUNT_PASSWORD",
        "OPENMU_DB_FRIEND_PASSWORD",
        "OPENMU_DB_GUILD_PASSWORD",
    };

    private readonly Dictionary<string, string?> originalValues = new();

    /// <summary>
    /// Sets placeholder passwords before any test context is constructed.
    /// </summary>
    [OneTimeSetUp]
    public void SetUp()
    {
        foreach (var variable in RequiredVariables)
        {
            this.originalValues[variable] = Environment.GetEnvironmentVariable(variable);
            Environment.SetEnvironmentVariable(variable, "test-placeholder");
        }
    }

    /// <summary>
    /// Restores the original (unset or inherited) environment variables after the run.
    /// </summary>
    [OneTimeTearDown]
    public void TearDown()
    {
        foreach (var pair in this.originalValues)
        {
            Environment.SetEnvironmentVariable(pair.Key, pair.Value);
        }
    }
}