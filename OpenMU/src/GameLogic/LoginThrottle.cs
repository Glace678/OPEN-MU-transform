// <copyright file="LoginThrottle.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic;

using System.Collections.Concurrent;

/// <summary>
/// Process-global brute-force throttle for sensitive account-gated actions (vault pin,
/// security code).
/// </summary>
/// <remarks>
/// The state is keyed by account login name so that a client disconnect/reconnect within the
/// same server process no longer resets the failure counter — previously the counter lived on
/// the per-session <see cref="Player"/> object, so dropping the connection and logging back in
/// gave an attacker a fresh set of attempts.
/// <para>
/// The state is intentionally kept in memory rather than in the database: persisting it would
/// require a schema migration (new columns on the account table), while the practical attack
/// (reconnect to reset) is fully closed by a process-global store. The state only clears on a
/// server restart, which is a much higher bar for an attacker.
/// </para>
/// </remarks>
public static class LoginThrottle
{
    private static readonly ConcurrentDictionary<string, ThrottleState> States = new(StringComparer.Ordinal);
    private static readonly object SyncRoot = new();

    /// <summary>
    /// Returns whether the given key is currently inside an active lockout window.
    /// </summary>
    /// <param name="key">The throttle key (namespaced account login name).</param>
    /// <param name="now">The current UTC time.</param>
    /// <returns><c>true</c> if attempts for the key are currently rejected.</returns>
    public static bool IsLockedOut(string key, DateTime now)
    {
        lock (SyncRoot)
        {
            return States.TryGetValue(key, out var state)
                   && state.LockoutUntil is { } until
                   && now < until;
        }
    }

    /// <summary>
    /// Registers a failed attempt: increments the counter and, when the maximum is reached,
    /// starts a lockout window and resets the counter.
    /// </summary>
    /// <param name="key">The throttle key (namespaced account login name).</param>
    /// <param name="now">The current UTC time.</param>
    /// <param name="maxFailedAttempts">The number of failures that triggers a lockout.</param>
    /// <param name="lockoutDuration">The lockout duration after the threshold is reached.</param>
    public static void RegisterFailure(string key, DateTime now, int maxFailedAttempts, TimeSpan lockoutDuration)
    {
        lock (SyncRoot)
        {
            var state = States.GetOrAdd(key, _ => new ThrottleState());
            state.FailCount++;
            if (state.FailCount >= maxFailedAttempts)
            {
                state.LockoutUntil = now.Add(lockoutDuration);
                state.FailCount = 0;
            }
        }
    }

    /// <summary>
    /// Resets the throttle state for a key after a successful verification.
    /// </summary>
    /// <param name="key">The throttle key (namespaced account login name).</param>
    public static void Reset(string key)
    {
        lock (SyncRoot)
        {
            States.TryRemove(key, out _);
        }
    }

    private sealed class ThrottleState
    {
        public int FailCount { get; set; }

        public DateTime? LockoutUntil { get; set; }
    }
}
