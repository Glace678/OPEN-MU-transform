namespace OpenMu.Balance;

public sealed record TokenState(DateOnly Day, int EarnedToday, long Balance,
    Dictionary<string, DateTimeOffset> LastBossClaim, HashSet<string> ClaimedRewardIds);
public sealed record TokenClaim(bool Granted, int Tokens, string Reason, TokenState State);
public sealed record EventReward(double TotalExperienceBudget, long TotalZenBudget, int Tokens);
public sealed record WalletResult(bool Success, long Balance, string Reason);

public sealed class Rewards(Rules rules)
{
    private Design D => rules.Design;

    public string EquipmentClass(string killerClass, double roll)
    {
        Rules.RollGuard(roll);
        if (!D.Classes.Any(c => c.Id == killerClass)) throw new ArgumentException("Unknown class.");
        if (roll < D.Loot.SmartClassChance) return killerClass;
        var others = D.Classes.Where(c => c.Id != killerClass).ToArray();
        var index = (int)((roll - D.Loot.SmartClassChance) / (1 - D.Loot.SmartClassChance) * others.Length);
        return others[Math.Min(index, others.Length - 1)].Id;
    }

    public EventReward EventBudget(string eventId, double contentRank)
    {
        var spec = D.Events.Single(e => e.Id == eventId);
        // This is the entire instance budget: distributed kill XP plus completion XP, not two rewards.
        return new EventReward(rules.BaseExperience(contentRank) * spec.RewardKillEquivalents,
            checked((long)Math.Round(rules.BaseZen(contentRank) * D.Loot.MoneyChance * spec.RewardKillEquivalents)),
            spec.Tokens);
    }

    public TokenClaim Claim(TokenState state, string rewardId, string sourceKey, int requestedTokens,
        DateTimeOffset now, bool boss)
    {
        if (string.IsNullOrWhiteSpace(rewardId) || string.IsNullOrWhiteSpace(sourceKey) || requestedTokens <= 0
            || requestedTokens > D.Loot.EventTokenCapPerDay || state.Balance < 0 || state.EarnedToday < 0)
            throw new ArgumentException("Invalid token claim.");
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        if (today < state.Day) return new TokenClaim(false, 0, "clock-regression", state);
        if (state.ClaimedRewardIds.Contains(rewardId)) return new TokenClaim(false, 0, "already-claimed", state);
        if (boss && state.LastBossClaim.TryGetValue(sourceKey, out var last)
                 && (now - last).TotalMinutes < D.Loot.BossTokenCooldownMinutes)
            return new TokenClaim(false, 0, "boss-cooldown", state);
        var earned = today == state.Day ? state.EarnedToday : 0;
        var tokens = Math.Min(requestedTokens, Math.Max(0, D.Loot.EventTokenCapPerDay - earned));
        var claims = new HashSet<string>(state.ClaimedRewardIds, StringComparer.Ordinal) { rewardId };
        var lastBoss = new Dictionary<string, DateTimeOffset>(state.LastBossClaim, StringComparer.Ordinal);
        if (boss) lastBoss[sourceKey] = now;
        var next = new TokenState(today, earned + tokens, checked(state.Balance + tokens), lastBoss, claims);
        return new TokenClaim(tokens > 0, tokens, tokens > 0 ? "granted" : "daily-cap", next);
    }

    public WalletResult Purchase(long balance, long unitPrice, int quantity)
    {
        if (balance < 0 || balance > D.Economy.WalletCap || unitPrice < 0 || quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(balance));
        if (unitPrice > balance / quantity) return new WalletResult(false, balance, "insufficient-funds");
        return new WalletResult(true, balance - unitPrice * quantity, "purchased");
    }

    public WalletResult Credit(long balance, long reward)
    {
        if (balance < 0 || balance > D.Economy.WalletCap || reward < 0)
            throw new ArgumentOutOfRangeException(nameof(balance));
        // Excess must remain as a claimable reward; do not silently destroy it.
        if (reward > D.Economy.WalletCap - balance) return new WalletResult(false, balance, "claim-later-wallet-full");
        return new WalletResult(true, balance + reward, "credited");
    }
}
