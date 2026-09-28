// <copyright file="AccountRecoveryServiceTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.Tests;

using System.Threading;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.Web.AdminPanel.API;

/// <summary>Tests proof verification, credential rotation and concurrent one-time consumption.</summary>
[TestFixture]
public class AccountRecoveryServiceTests
{
    /// <summary>Recovery codes are independently generated and hash to a different stored value.</summary>
    [Test]
    public void GeneratedCodesAreDistinctAndCanonical()
    {
        var codes = Enumerable.Range(0, 32).Select(_ => AccountRecoveryService.GenerateCode()).ToArray();
        Assert.That(codes.Distinct().Count(), Is.EqualTo(codes.Length));
        foreach (var code in codes)
        {
            Assert.That(code.Split('-').Select(group => group.Length), Is.All.EqualTo(8));
            Assert.That(AccountRecoveryService.TryHashCode(code, out var hash), Is.True);
            Assert.That(hash.Length, Is.EqualTo(64));
            Assert.That(hash, Is.Not.EqualTo(code.Replace("-", string.Empty, StringComparison.Ordinal)));
            Assert.That(AccountRecoveryService.TryHashCode(code.ToLowerInvariant().Replace("-", string.Empty, StringComparison.Ordinal), out var normalizedHash), Is.True);
            Assert.That(normalizedHash, Is.EqualTo(hash));
        }
    }

    /// <summary>Malformed credentials cannot reach the storage read.</summary>
    [TestCase(null)]
    [TestCase("")]
    [TestCase("123456")]
    [TestCase("not-a-recovery-code")]
    public void ShortCodesAreRejected(string? code)
    {
        Assert.That(AccountRecoveryService.TryHashCode(code, out var hash), Is.False);
        Assert.That(hash, Is.Empty);
    }

    /// <summary>Length, Unicode and character validation fail closed.</summary>
    [Test]
    public void InvalidRepresentationsAreRejected()
    {
        foreach (var code in new[] { new string('G', 64), new string('A', 81), new string('\uff21', 64), new string('A', 32) + " " + new string('A', 32) })
        {
            Assert.That(AccountRecoveryService.TryHashCode(code, out _), Is.False);
        }
    }

    /// <summary>An unknown, unenrolled or malformed stored credential never becomes a recovery proof.</summary>
    [TestCase(null)]
    [TestCase("")]
    [TestCase("invalid-stored-hash")]
    public async Task UnenrolledOrMalformedHashCannotRecoverAsync(string? storedHash)
    {
        var context = CreateContext(storedHash);
        var original = context.Snapshot;
        var result = await AccountRecoveryService.ResetAsync(context, "solotest", AccountRecoveryService.GenerateCode(), "new-password").ConfigureAwait(false);
        Assert.That(result, Is.Null);
        Assert.That(context.Snapshot, Is.EqualTo(original));
        Assert.That(context.Replacements, Is.Zero);
    }

    /// <summary>Correct proof changes the password, rotates the code, and invalidates replay.</summary>
    [Test]
    public async Task RecoveryChangesPasswordAndConsumesCodeOnceAsync()
    {
        var originalCode = AccountRecoveryService.GenerateCode();
        _ = AccountRecoveryService.TryHashCode(originalCode, out var originalHash);
        var context = CreateContext(originalHash);

        var replacement = await AccountRecoveryService.ResetAsync(context, "solotest", originalCode, "new-password").ConfigureAwait(false);

        Assert.That(replacement, Is.Not.Null);
        _ = AccountRecoveryService.TryHashCode(replacement, out var replacementHash);
        Assert.That(context.Snapshot!.RecoveryCodeHash, Is.EqualTo(replacementHash));
        Assert.That(BCrypt.Net.BCrypt.Verify("new-password", context.Snapshot.PasswordHash), Is.True);
        Assert.That(BCrypt.Net.BCrypt.Verify("old-password", context.Snapshot.PasswordHash), Is.False);
        Assert.That(context.Snapshot.RecoveryCodeHash, Is.Not.EqualTo(originalHash));
        var replay = await AccountRecoveryService.ResetAsync(context, "solotest", originalCode, "attacker-password").ConfigureAwait(false);
        Assert.That(replay, Is.Null);
        Assert.That(context.Replacements, Is.EqualTo(1));
    }

    /// <summary>A well-formed but different code cannot update credentials.</summary>
    [Test]
    public async Task WrongProofCannotRecoverAsync()
    {
        _ = AccountRecoveryService.TryHashCode(AccountRecoveryService.GenerateCode(), out var hash);
        var context = CreateContext(hash);
        var original = context.Snapshot;
        var result = await AccountRecoveryService.ResetAsync(context, "solotest", AccountRecoveryService.GenerateCode(), "new-password").ConfigureAwait(false);
        Assert.That(result, Is.Null);
        Assert.That(context.Snapshot, Is.EqualTo(original));
        Assert.That(context.Replacements, Is.Zero);
    }

    /// <summary>Old accounts opt in only after proving the current password; the existing password is not changed.</summary>
    [Test]
    public async Task LegacyAccountEnrollmentRequiresPasswordAsync()
    {
        var context = CreateContext(null);
        var passwordHash = context.Snapshot!.PasswordHash;
        Assert.That(await AccountRecoveryService.IssueAsync(context, "solotest", "wrong-password").ConfigureAwait(false), Is.Null);
        Assert.That(context.Replacements, Is.Zero);
        Assert.That(context.Snapshot.RecoveryCodeHash, Is.Null);
        var issued = await AccountRecoveryService.IssueAsync(context, "solotest", "old-password").ConfigureAwait(false);
        Assert.That(issued, Is.Not.Null);
        Assert.That(context.Snapshot.PasswordHash, Is.EqualTo(passwordHash));
        _ = AccountRecoveryService.TryHashCode(issued, out var hash);
        Assert.That(context.Snapshot.RecoveryCodeHash, Is.EqualTo(hash));
        Assert.That(await AccountRecoveryService.ResetAsync(context, "solotest", issued!, "new-password").ConfigureAwait(false), Is.Not.Null);
    }

    /// <summary>Two requests authorized by the same old code produce exactly one committed replacement.</summary>
    [Test]
    public async Task TwoConcurrentRecoveriesHaveExactlyOneWinnerAsync()
    {
        var originalCode = AccountRecoveryService.GenerateCode();
        _ = AccountRecoveryService.TryHashCode(originalCode, out var hash);
        var context = CreateContext(hash, synchronizeReads: true);
        var tasks = new[]
        {
            AccountRecoveryService.ResetAsync(context, "solotest", originalCode, "new-password-one"),
            AccountRecoveryService.ResetAsync(context, "solotest", originalCode, "new-password-two"),
        };
        var results = await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);
        Assert.That(results.Count(result => result is not null), Is.EqualTo(1));
        Assert.That(context.Replacements, Is.EqualTo(1));
        var winner = results[0] is not null ? 0 : 1;
        Assert.That(BCrypt.Net.BCrypt.Verify(winner == 0 ? "new-password-one" : "new-password-two", context.Snapshot!.PasswordHash), Is.True);
        _ = AccountRecoveryService.TryHashCode(results[winner], out var replacementHash);
        Assert.That(context.Snapshot.RecoveryCodeHash, Is.EqualTo(replacementHash));
    }

    /// <summary>Two password-authorized enrollment requests cannot overwrite one another with two successful responses.</summary>
    [Test]
    public async Task TwoConcurrentEnrollmentsHaveExactlyOneWinnerAsync()
    {
        var context = CreateContext(null, synchronizeReads: true);
        var results = await Task.WhenAll(
            AccountRecoveryService.IssueAsync(context, "solotest", "old-password"),
            AccountRecoveryService.IssueAsync(context, "solotest", "old-password"))
            .WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);
        Assert.That(results.Count(result => result is not null), Is.EqualTo(1));
        Assert.That(context.Replacements, Is.EqualTo(1));
        Assert.That(BCrypt.Net.BCrypt.Verify("old-password", context.Snapshot!.PasswordHash), Is.True);
    }

    /// <summary>Storage rejection does not return a usable uncommitted replacement code.</summary>
    [Test]
    public async Task FailedCompareAndSwapDoesNotIssueCodeAsync()
    {
        var code = AccountRecoveryService.GenerateCode();
        _ = AccountRecoveryService.TryHashCode(code, out var hash);
        var context = CreateContext(hash);
        context.RejectReplacements = true;
        var original = context.Snapshot;
        Assert.That(await AccountRecoveryService.ResetAsync(context, "solotest", code, "new-password").ConfigureAwait(false), Is.Null);
        Assert.That(await AccountRecoveryService.IssueAsync(context, "solotest", "old-password").ConfigureAwait(false), Is.Null);
        Assert.That(context.Snapshot, Is.EqualTo(original));
    }

    private static AtomicCredentialContext CreateContext(string? recoveryHash, bool synchronizeReads = false) => new(
        new AccountCredentialSnapshot("solotest", BCrypt.Net.BCrypt.HashPassword("old-password"), recoveryHash), synchronizeReads);

    private sealed class AtomicCredentialContext : IAccountCredentialContext
    {
        private readonly object _gate = new();
        private readonly TaskCompletionSource _bothRead = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly bool _synchronizeReads;
        private int _readCount;

        public AtomicCredentialContext(AccountCredentialSnapshot snapshot, bool synchronizeReads)
        {
            this.Snapshot = snapshot;
            this._synchronizeReads = synchronizeReads;
        }

        public AccountCredentialSnapshot Snapshot { get; private set; }

        public int Replacements { get; private set; }

        public bool RejectReplacements { get; set; }

        public async ValueTask<AccountCredentialSnapshot?> ReadCredentialsAsync(string loginName, CancellationToken cancellationToken = default)
        {
            AccountCredentialSnapshot? snapshot;
            lock (this._gate)
            {
                snapshot = this.Snapshot?.LoginName == loginName ? this.Snapshot : null;
            }

            if (this._synchronizeReads)
            {
                if (Interlocked.Increment(ref this._readCount) == 2)
                {
                    this._bothRead.TrySetResult();
                }

                await this._bothRead.Task.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);
            }

            return snapshot;
        }

        public ValueTask<bool> TryReplaceCredentialsAsync(AccountCredentialSnapshot expected, string newPasswordHash, string? newRecoveryCodeHash, CancellationToken cancellationToken = default)
        {
            lock (this._gate)
            {
                if (this.RejectReplacements || this.Snapshot != expected)
                {
                    return ValueTask.FromResult(false);
                }

                this.Snapshot = expected with { PasswordHash = newPasswordHash, RecoveryCodeHash = newRecoveryCodeHash };
                this.Replacements++;
                return ValueTask.FromResult(true);
            }
        }
    }
}
