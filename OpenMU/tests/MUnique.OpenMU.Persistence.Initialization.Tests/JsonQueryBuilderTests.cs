// <copyright file="JsonQueryBuilderTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Tests;

using System.Diagnostics;
using System.IO;
using Microsoft.EntityFrameworkCore;
using MUnique.OpenMU.Persistence.EntityFramework;
using MUnique.OpenMU.Persistence.EntityFramework.Json;
using Account = MUnique.OpenMU.Persistence.EntityFramework.Model.Account;
using GameConfiguration = MUnique.OpenMU.Persistence.EntityFramework.Model.GameConfiguration;

/// <summary>
/// Tests for the <see cref="JsonQueryBuilder"/>.
/// </summary>
[TestFixture]
internal class JsonQueryBuilderTests
{
    /// <summary>
    /// Sets up this instance.
    /// </summary>
    [OneTimeSetUp]
    public void Setup()
    {
        if (!ConnectionConfigurator.IsInitialized)
        {
            ConnectionConfigurator.Initialize(new ConfigFileDatabaseConnectionStringProvider());
        }
    }

    /// <summary>
    /// Tests the json query builder for the <see cref="GameConfiguration"/> type.
    /// </summary>
    [Test]
    public void JsonQueryBuilderGameConfiguration()
    {
        using var installationContext = new ConfigurationContext();
        var type = installationContext.Model.GetEntityTypes().FirstOrDefault(t => t.ClrType == typeof(GameConfiguration));
        var builder = new GameConfigurationJsonQueryBuilder();
        var result = builder.BuildJsonQueryForEntity(type!);

        // Real structural assertions on the generated Postgres JSON SQL. This test used
        // to only prepend a timing comment and never assert, so a broken/empty builder
        // output stayed green (T5-03): now a malformed query returns red.
        Assert.That(result, Is.Not.Null.Or.Empty);
        Assert.That(result.TrimStart(), Does.StartWith("select"));
        Assert.That(result, Does.Contain("row_to_json"));
        Assert.That(result, Does.Contain(type!.GetTableName()));
        Assert.That(result.TrimEnd(), Does.EndWith("result"));
        Assert.That(result.Count(ch => ch == '('), Is.EqualTo(result.Count(ch => ch == ')')));
        // GameConfiguration owns collections; the root must embed at least one json array.
        Assert.That(result, Does.Contain("array_to_json"));
    }

    /// <summary>
    /// Tests the json query builder for the <see cref="Account"/> type.
    /// </summary>
    [Test]
    public void JsonQueryBuilderAccount()
    {
        using var installationContext = new ConfigurationContext();
        var type = installationContext.Model.GetEntityTypes().FirstOrDefault(t => t.ClrType == typeof(Account));
        var builder = new JsonQueryBuilder();
        var result = builder.BuildJsonQueryForEntity(type!);

        // Real structural assertions on the generated Postgres JSON SQL (T5-03).
        Assert.That(result, Is.Not.Null.Or.Empty);
        Assert.That(result.TrimStart(), Does.StartWith("select"));
        Assert.That(result, Does.Contain("row_to_json"));
        Assert.That(result, Does.Contain(type!.GetTableName()));
        Assert.That(result.TrimEnd(), Does.EndWith("result"));
        Assert.That(result.Count(ch => ch == '('), Is.EqualTo(result.Count(ch => ch == ')')));
    }

    /// <summary>
    /// Loads the <see cref="GameConfiguration"/> using the <see cref="JsonQueryBuilder"/> and the <see cref="JsonObjectLoader"/>.
    /// This hits a real PostgreSQL database. It only runs when OPENMU_INTEGRATION_DB is set;
    /// otherwise it is explicitly reported as inconclusive instead of silently passing (T5-03).
    /// </summary>
    [Test]
    [Category("Integration")]
    public async Task LoadConfigByJsonAsync()
    {
        if (string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("OPENMU_INTEGRATION_DB")))
        {
            Assert.Inconclusive("Set OPENMU_INTEGRATION_DB to a PostgreSQL connection string to run the JsonObjectLoader integration test.");
        }

        await using var installationContext = new ConfigurationContext();
        installationContext.Database.OpenConnection();
        var builder = new GameConfigurationJsonObjectLoader();
        var result = (await builder.LoadAllObjectsAsync<EntityFramework.Model.GameConfiguration>(installationContext).ConfigureAwait(false)).ToList();

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Count, Is.Not.EqualTo(0));
    }
}
