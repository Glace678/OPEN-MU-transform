// <copyright file="PotionCooldownTracker.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic;

/// <summary>
/// Tracks per-resource potion cooldowns atomically, so a consumed recovery item
/// blocks only its own resource group and concurrent consumption cannot overlap.
/// </summary>
internal sealed class PotionCooldownTracker
{
    private readonly object _lock = new();
    private readonly Dictionary<BalanceV1.PotionGroup, DateTime> _cooldowns = new();

    /// <summary>
    /// Reserves the cooldown for the specified group when it is not already cooling down.
    /// </summary>
    /// <param name="group">The independently cooled-down potion resource.</param>
    /// <param name="cooldown">The cooldown duration.</param>
    /// <param name="now">The timestamp used for the decision.</param>
    /// <returns><see langword="true"/> when the group was available and is now reserved.</returns>
    public bool TryBegin(BalanceV1.PotionGroup group, TimeSpan cooldown, DateTime now)
    {
        lock (this._lock)
        {
            if (this._cooldowns.TryGetValue(group, out var cooldownUntil) && cooldownUntil > now)
            {
                return false;
            }

            this._cooldowns[group] = now.Add(cooldown);
            return true;
        }
    }
}
