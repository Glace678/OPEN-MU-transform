// <copyright file="ResetProgressionCalculator.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Resets;

/// <summary>
/// Calculates costs and rewards for the next character reset.
/// </summary>
public static class ResetProgressionCalculator
{
    /// <summary>
    /// Calculates the reset progression for the next reset.
    /// </summary>
    /// <param name="currentResetCount">The current reset count.</param>
    /// <param name="pointsPerResetOverride">The player-specific points per reset override (0 means not configured).</param>
    /// <param name="configuration">The reset configuration.</param>
    /// <returns>The calculated progression.</returns>
    public static ResetProgression Calculate(int currentResetCount, int pointsPerResetOverride, ResetConfiguration configuration)
    {
        var sanitizedCurrentResets = Math.Max(0, currentResetCount);
        var nextResetCount = (int)Math.Min(int.MaxValue, (long)sanitizedCurrentResets + 1);
        long requiredZenLong = Math.Max(0, configuration.RequiredMoney);
        if (configuration.MultiplyRequiredMoneyByResetCount)
        {
            requiredZenLong *= nextResetCount;
        }

        var requiredZen = (int)Math.Min(int.MaxValue, requiredZenLong);
        var pointsForReset = GetPointsForReset(configuration, pointsPerResetOverride, nextResetCount);
        var totalPointsAfterReset = GetTotalPointsAfterReset(configuration, pointsPerResetOverride, nextResetCount, pointsForReset);
        var requiredItemAmount = GetRequiredItemAmount(configuration, nextResetCount);

        return new ResetProgression(nextResetCount, requiredZen, requiredItemAmount, pointsForReset, totalPointsAfterReset);
    }

    private static int GetPointsForReset(ResetConfiguration configuration, int pointsPerResetOverride, int nextResetCount)
    {
        if (GetMatchingTier(configuration.PointsTiers, nextResetCount, tier => tier.MinimumResetCount) is { } tier)
        {
            return Math.Max(0, tier.PointsGranted);
        }

        long pointsPerReset = Math.Max(0, pointsPerResetOverride == 0 ? configuration.PointsPerReset : pointsPerResetOverride);
        if (configuration.MultiplyPointsByResetCount)
        {
            pointsPerReset *= nextResetCount;
        }

        return (int)Math.Min(int.MaxValue, pointsPerReset);
    }

    private static int GetRequiredItemAmount(ResetConfiguration configuration, int nextResetCount)
    {
        if (configuration.RequiredResetItem is null)
        {
            return 0;
        }

        if (GetMatchingTier(configuration.ItemCostTiers, nextResetCount, tier => tier.MinimumResetCount) is not { } tier)
        {
            return 0;
        }

        return Math.Max(0, tier.RequiredItemAmount);
    }

    private static int GetTotalPointsAfterReset(ResetConfiguration configuration, int pointsPerResetOverride, int nextResetCount, int pointsForReset)
    {
        if (configuration.PointsTiers.Count == 0)
        {
            return pointsForReset;
        }

        // Each tier is constant over its entire range. Iterating every reset
        // can take billions of steps and overflow the loop counter at int.MaxValue.
        // Preserve the first configured tier when minimum counts are equal.
        long total = 0;
        long start = 1;
        int? tierPoints = null;
        var legacyPoints = Math.Max(0, pointsPerResetOverride == 0 ? configuration.PointsPerReset : pointsPerResetOverride);

        foreach (var tier in configuration.PointsTiers
                     .GroupBy(t => t.MinimumResetCount)
                     .Select(g => g.First())
                     .OrderBy(t => t.MinimumResetCount))
        {
            if (tier.MinimumResetCount > nextResetCount)
            {
                break;
            }

            var boundary = Math.Max(1L, tier.MinimumResetCount);
            total += GetRangePoints(boundary - start, tierPoints);
            if (total >= int.MaxValue)
            {
                return int.MaxValue;
            }

            start = boundary;
            tierPoints = Math.Max(0, tier.PointsGranted);
        }

        total += GetRangePoints((long)nextResetCount - start + 1, tierPoints);
        return (int)Math.Min(int.MaxValue, total);

        long GetRangePoints(long count, int? points)
        {
            if (points is { } fixedPoints)
            {
                return count * fixedPoints;
            }

            // The legacy range always starts at reset 1. Cap before multiplying
            // to avoid overflow even when both the count and reward are maximal.
            var factor = configuration.MultiplyPointsByResetCount ? count * (count + 1) / 2 : count;
            return legacyPoints > 0 && factor > int.MaxValue / legacyPoints
                ? int.MaxValue
                : factor * legacyPoints;
        }
    }

    private static TTier? GetMatchingTier<TTier>(IEnumerable<TTier> tiers, int resetCount, Func<TTier, int> getMinimumResetCount)
        where TTier : class
    {
        return tiers
            .OrderByDescending(getMinimumResetCount)
            .FirstOrDefault(tier => getMinimumResetCount(tier) <= resetCount);
    }
}
