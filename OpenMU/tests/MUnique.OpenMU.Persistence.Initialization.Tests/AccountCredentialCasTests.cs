// <copyright file="AccountCredentialCasTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Tests;

using System.Data.Common;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging.Abstractions;
using MUnique.OpenMU.Persistence.EntityFramework;
using MUnique.OpenMU.Persistence.EntityFramework.Model;

/// <summary>Checks recovery storage and the actual EF update statement without opening a database connection.</summary>
[TestFixture]
internal class AccountCredentialCasTests
{
    /// <summary>The offline migration script only adds nullable storage and never backfills credentials.</summary>
    [Test]
    public void RecoveryMigrationGeneratesOnlyNullableAccountDdl()
    {
        using var context = new EntityDataContext();
        var script = context.GetService<IMigrator>().GenerateScript(
            "20260905000100_AddSoloCashShopData", "20260926000100_AddAccountRecoveryCodeHash");
        Assert.That(script, Does.Contain("ADD \"RecoveryCodeHash\" character varying(64)"));
        Assert.That(script, Does.Not.Contain("NOT NULL"));
        Assert.That(script, Does.Not.Contain("UPDATE"));
        Assert.That(script, Does.Not.Contain("SecurityCode"));
        Assert.That(script, Does.Not.Contain("PasswordHash"));
    }

    /// <summary>The additive migration leaves existing accounts nullable and does not add a gameplay concurrency token.</summary>
    [Test]
    public void RecoveryFieldMatchesNullableMigrationSnapshot()
    {
        using var context = new EntityDataContext();
        var property = context.Model.FindEntityType(typeof(Account))!.FindProperty(nameof(Account.RecoveryCodeHash));
        Assert.That(property, Is.Not.Null);
        Assert.That(property!.IsNullable, Is.True);
        Assert.That(property.GetMaxLength(), Is.EqualTo(64));
        Assert.That(property.IsConcurrencyToken, Is.False);
        Assert.That(context.Database.GetMigrations(), Does.Contain("20260926000100_AddAccountRecoveryCodeHash"));
        Assert.That(context.Database.HasPendingModelChanges(), Is.False);
    }

    /// <summary>The same SQL compare-and-swap snapshot yields exactly one affected row across two concurrent contexts.</summary>
    [TestCase(null)]
    [TestCase("OLD_RECOVERY_HASH")]
    [TestCase(null, true)]
    [TestCase("OLD_RECOVERY_HASH", true)]
    public async Task ConcurrentEfCompareAndSwapHasExactlyOneWinnerAsync(string? oldRecoveryHash, bool revoke = false)
    {
        var expected = new AccountCredentialSnapshot("solotest", "OLD_PASSWORD_HASH", oldRecoveryHash);
        var observer = new CasCommandObserver(expected);
        using var firstDb = new SqlProbeContext(observer);
        using var secondDb = new SqlProbeContext(observer);
        using var first = CreatePlayerContext(firstDb);
        using var second = CreatePlayerContext(secondDb);

        var results = await Task.WhenAll(
            first.TryReplaceCredentialsAsync(expected, "NEW_PASSWORD_ONE", revoke ? null : "NEW_RECOVERY_ONE").AsTask(),
            second.TryReplaceCredentialsAsync(expected, "NEW_PASSWORD_TWO", revoke ? null : "NEW_RECOVERY_TWO").AsTask())
            .WaitAsync(TimeSpan.FromSeconds(15)).ConfigureAwait(false);

        Assert.That(results.Count(result => result), Is.EqualTo(1));
        Assert.That(observer.Commands, Has.Count.EqualTo(2));
        Assert.That(observer.OpenAttemptsSuppressed, Is.EqualTo(2));
        var winner = results[0] ? "ONE" : "TWO";
        Assert.That(observer.Current.PasswordHash, Is.EqualTo("NEW_PASSWORD_" + winner));
        Assert.That(observer.Current.RecoveryCodeHash, Is.EqualTo(revoke ? null : "NEW_RECOVERY_" + winner));
        foreach (var sql in observer.Commands)
        {
            var where = sql[sql.IndexOf("WHERE", StringComparison.Ordinal)..];
            Assert.That(where, Does.Contain("\"LoginName\""));
            Assert.That(where, Does.Contain("\"PasswordHash\""));
            Assert.That(where, Does.Contain("\"RecoveryCodeHash\""));
            if (oldRecoveryHash is null)
            {
                Assert.That(where, Does.Contain("IS NULL"));
            }
        }
    }

    private static PlayerContext CreatePlayerContext(DbContext context) => new(
        context,
        new CacheAwareRepositoryProvider(NullLoggerFactory.Instance, null),
        NullLogger<PlayerContext>.Instance);

    private sealed class SqlProbeContext : EntityDataContext
    {
        private readonly CasCommandObserver _observer;

        public SqlProbeContext(CasCommandObserver observer)
        {
            this._observer = observer;
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            // A reserved nonexistent host and suppressed open make accidental real
            // database access impossible. SQL generation still uses Npgsql itself.
            optionsBuilder.UseNpgsql("Host=never-connect.invalid;Database=unit-test;Username=unit-test;Password=unused;Timeout=1")
                .AddInterceptors(new NeverOpenConnection(this._observer), this._observer);
        }
    }

    private sealed class NeverOpenConnection : DbConnectionInterceptor
    {
        private readonly CasCommandObserver _observer;

        public NeverOpenConnection(CasCommandObserver observer)
        {
            this._observer = observer;
        }

        public override ValueTask<InterceptionResult> ConnectionOpeningAsync(
            DbConnection connection,
            ConnectionEventData eventData,
            InterceptionResult result,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref this._observer.OpenAttemptsSuppressed);
            return ValueTask.FromResult(InterceptionResult.Suppress());
        }
    }

    private sealed class CasCommandObserver : DbCommandInterceptor
    {
        private readonly object _gate = new();
        private readonly TaskCompletionSource _bothCommands = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _arrivals;

        public CasCommandObserver(AccountCredentialSnapshot expected)
        {
            this.Current = expected;
        }

        public int OpenAttemptsSuppressed;

        public AccountCredentialSnapshot Current { get; private set; }

        public List<string> Commands { get; } = new();

        public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref this._arrivals) == 2)
            {
                this._bothCommands.TrySetResult();
            }

            await this._bothCommands.Task.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);
            lock (this._gate)
            {
                this.Commands.Add(command.CommandText);
                Assert.That(command.CommandText, Does.StartWith("UPDATE"));
                var where = command.CommandText[command.CommandText.IndexOf("WHERE", StringComparison.Ordinal)..];
                var expectedRecovery = where.Contains("IS NULL", StringComparison.Ordinal)
                    ? null : Parameter(command, "expected_RecoveryCodeHash");
                var expected = new AccountCredentialSnapshot(
                    Parameter(command, "expected_LoginName"),
                    Parameter(command, "expected_PasswordHash"), expectedRecovery);
                if (this.Current != expected)
                {
                    return InterceptionResult<int>.SuppressWithResult(0);
                }

                this.Current = expected with
                {
                    PasswordHash = UpdateParameter(command, "PasswordHash")!,
                    RecoveryCodeHash = UpdateParameter(command, "RecoveryCodeHash"),
                };
                return InterceptionResult<int>.SuppressWithResult(1);
            }
        }

        private static string Parameter(DbCommand command, string nameFragment) => (string)command.Parameters
            .Cast<DbParameter>().Single(parameter => parameter.ParameterName.Contains(nameFragment, StringComparison.Ordinal)).Value!;

        private static string? UpdateParameter(DbCommand command, string column)
        {
            var setClause = command.CommandText[..command.CommandText.IndexOf("WHERE", StringComparison.Ordinal)];
            if (Regex.IsMatch(setClause, "\"" + column + "\"\\s*=\\s*NULL"))
            {
                return null;
            }

            var match = Regex.Match(setClause, "\"" + column + "\"\\s*=\\s*@(?<parameter>\\w+)");
            Assert.That(match.Success, Is.True, command.CommandText);
            var value = command.Parameters.Cast<DbParameter>()
                .Single(parameter => parameter.ParameterName.TrimStart('@') == match.Groups["parameter"].Value).Value;
            return value is DBNull ? null : (string)value!;
        }
    }
}
