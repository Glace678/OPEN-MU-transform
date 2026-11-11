// <copyright file="PotionCooldownTracker.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic;

using System.Collections.Concurrent;

/// <summary>
/// Tracks per-resource potion cooldowns atomically, so a consumed recovery item
/// blocks only its own resource group and concurrent consumption cannot overlap.
/// </summary>
internal sealed class PotionCooldownTracker
{
    private static readonly ConcurrentDictionary<(Guid CharacterId, BalanceV1.PotionGroup Group), DateTime> CharacterCooldowns = new();
    private readonly Func<Guid?>? _characterIdProvider;
    private readonly object _lock = new();
    private readonly Dictionary<BalanceV1.PotionGroup, DateTime> _cooldowns = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="PotionCooldownTracker"/> class.
    /// </summary>
    /// <param name="characterIdProvider">Optional provider for the active character identifier so cooldowns survive relog.</param>
    public PotionCooldownTracker(Func<Guid?>? characterIdProvider = null)
    {
        this._characterIdProvider = characterIdProvider;
    }

    /// <summary>
    /// Reserves the cooldown for the specified group when it is not already cooling down.
    /// </summary>
    /// <param name="group">The independently cooled-down potion resource.</param>
    /// <param name="cooldown">The cooldown duration.</param>
    /// <param name="now">The timestamp used for the decision.</param>
    /// <returns><see langword="true"/> when the group was available and is now reserved.</returns>
    public bool TryBegin(BalanceV1.PotionGroup group, TimeSpan cooldown, DateTime now)
    {
        var characterId = this._characterIdProvider?.Invoke();
        lock (this._lock)
        {
            if (this._cooldowns.TryGetValue(group, out var cooldownUntil) && cooldownUntil > now)
            {
                return false;
            }

            if (characterId is { } id && id != Guid.Empty)
            {
                var key = (id, group);
                while (true)
                {
                    if (CharacterCooldowns.TryGetValue(key, out var sharedUntil))
                    {
                        if (sharedUntil > now)
                        {
                            this._cooldowns[group] = sharedUntil;
                            return false;
                        }

                        var nextUntil = now.Add(cooldown);
                        if (CharacterCooldowns.TryUpdate(key, nextUntil, sharedUntil))
                        {
                            this._cooldowns[group] = nextUntil;
                            return true;
                        }

                        continue;
                    }

                    var initialUntil = now.Add(cooldown);
                    if (CharacterCooldowns.TryAdd(key, initialUntil))
                    {
                        this._cooldowns[group] = initialUntil;
                        if (CharacterCooldowns.Count > 4096)
                        {
                            PruneExpired(now);
                        }

                        return true;
                    }
                }
            }

            this._cooldowns[group] = now.Add(cooldown);
            return true;
        }
    }

    private static void PruneExpired(DateTime now)
    {
        foreach (var entry in CharacterCooldowns)
        {
            if (entry.Value <= now)
            {
                CharacterCooldowns.TryRemove(entry);
            }
        }
    }
}
