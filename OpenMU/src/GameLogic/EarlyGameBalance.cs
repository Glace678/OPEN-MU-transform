// <copyright file="EarlyGameBalance.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic;

using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.GameLogic.Attributes;

/// <summary>
/// Profile-independent rules that keep the first few character levels playable.
/// </summary>
/// <remarks>
/// The legacy hit formula (<c>1 - defenseRate/attackRate</c>) collapses toward a
/// 3% floor whenever a monster's evasion exceeds a fresh character's
/// accuracy, and the legacy "overrate" branch then multiplies any damage by
/// 0.3. Together these make a brand-new character feel unable to kill even the
/// weakest monster, regardless of which experience profile is installed.
///
/// This class replaces those fragile edge cases for the early-game band with a
/// guaranteed, clearly bounded hit chance, and it suppresses the harsh
/// overrate damage penalty while the player is still inside that band. Nothing
/// here affects higher-level PvM or any PvP.
/// </remarks>
public static class EarlyGameBalance
{
    /// <summary>The highest normal level that receives the new-player guarantee.</summary>
    public const int ProtectedLevel = 20;

    /// <summary>The guaranteed minimum hit chance against monsters in the protected band.</summary>
    public const float MinimumHitChance = 0.80f;

    /// <summary>The highest normal level for which the legacy overrate penalty is waived.</summary>
    public const int OverrateWaiverLevel = 40;

    /// <summary>The damage multiplier the legacy branch applies against an overrating monster.</summary>
    public const float LegacyOverrateMultiplier = 0.3f;

    /// <summary>
    /// Returns the hit chance for a player attacking a monster, enforcing the early-game floor.
    /// </summary>
    /// <param name="playerLevel">The attacking player's normal level.</param>
    /// <param name="legacyHitChance">The hit chance produced by the legacy formula.</param>
    /// <returns>The effective hit chance, floored inside the protected band.</returns>
    public static float AdjustHitChance(int playerLevel, float legacyHitChance)
    {
        if (playerLevel > ProtectedLevel)
        {
            return legacyHitChance;
        }

        return Math.Max(legacyHitChance, MinimumHitChance);
    }

    /// <summary>
    /// Returns the damage multiplier against an overrating monster, waiving the legacy
    /// penalty while the player is still in the early-game band.
    /// </summary>
    /// <param name="playerLevel">The attacking player's normal level.</param>
    /// <returns>The overrate damage multiplier.</returns>
    public static float GetOverrateMultiplier(int playerLevel) =>
        playerLevel <= OverrateWaiverLevel ? 1.0f : LegacyOverrateMultiplier;

    /// <summary>
    /// Reads an attacker's normal level, treating a non-player attacker as unbounded.
    /// </summary>
    /// <param name="attacker">The attacking object.</param>
    /// <returns>The attacker's normal level.</returns>
    public static int GetAttackerLevel(IAttacker attacker)
    {
        if (attacker is Player)
        {
            return (int)attacker.Attributes[Stats.Level];
        }

        return int.MaxValue;
    }
}
