using System.Text.Json;

namespace OpenMu.Balance;

public sealed record Design(
    string Version, int NormalCap, int MasterCap, int PointsPerLevel, double MasterRankPerLevel,
    CombatRules Combat, GrowthRules Growth, Profile[] Profiles, Gear[] Gear, ClassSpec[] Classes,
    Skill[] Skills, MonsterRules Monsters, ProgressionRules Progression, LootRules Loot,
    EconomyRules Economy, EnhancementRules Enhancement, EventSpec[] Events, PvpRules Pvp, Route[] Routes)
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    public static Design Load(string file) => JsonSerializer.Deserialize<Design>(File.ReadAllText(file), Json)
        ?? throw new InvalidDataException("Empty balance design.");
    public Skill Skill(string id) => Skills.Single(s => s.Id == id);
    public MonsterRank Rank(string id) => Monsters.Ranks.Single(r => r.Id == id);
    public Profile Profile(string id) => Profiles.Single(p => p.Id == id);
}

public sealed record CombatRules(double HitBase, double HitSlope, double HitMin, double HitMax,
    double ArmorConstant, double ArmorPerRank, double ArmorCap, double CritBase, double CritCap,
    double CritMultiplier, double ExcellentCap, double ExcellentMultiplier, double TotalReductionCap,
    double PenetrationCap, double HasteCap, double MinimumActionSeconds, double InCombatRegen,
    double OutOfCombatRegen, double ManaRegen, int PackSize, double TravelSeconds,
    double ReferenceKillCycleSeconds, double BossTelegraphSeconds, double BossTelegraphMultiplier,
    double TrainedDodgeChance, double BeginnerDodgeChance);
public sealed record GrowthRules(double BaseHealth, double HealthPerLevel, double HealthPerVitality,
    double BaseMana, double ManaPerLevel, double ManaPerEnergy, double BasePower, double PowerPerRank,
    double GearPowerShare, double GearArmorShare, double ArmorPerAgility, double BaseArmor,
    double MasterDamageCap, double MasterHealthCap, double MasterArmorCap, double MasterManaCap,
    double PartyDamageBuffCap, double PartyDefenseBuffCap, double LifeStealCapPerSecond, double PetDamageBudget);
public sealed record Profile(string Id, double NormalXp, double MasterXp);
public sealed record Gear(string Id, double Factor, double Crit, double Excellent, double Reduction);
public sealed record ClassSpec(string Id, string Name, int[] EngineIds, double Health, double Damage,
    double Armor, double Mana, double RangedUptime, Build[] Builds);
public sealed record Build(string Id, string Name, double[] Allocation, double[] PowerWeights,
    string Single, string Area, double BonusReduction, double LifeSteal, double Support);
public sealed record Skill(string Id, int[] EngineIds, int Unlock, double Coefficient, double ActionSeconds,
    double ManaFraction, double ManaFlat, double[] HitWeights, double[] TargetWeights,
    RuntimeSkillContract? RuntimeContract = null);
public sealed record RuntimeSkillContract(int[] ClassIds, string Caller, string Category,
    int Mana, int Ability, int PayloadHits, string Unlock, string Effects, string ResourcePolicy);
public sealed record MonsterRules(double HpBase, double HpLinear, double HpQuadratic, double DamageBase,
    double DamageLinear, double ArmorBase, double ArmorLinear, MonsterRank[] Ranks, int[] BossIds,
    int[] EliteIds, double XpBase, double XpLinear, double XpQuadratic, double ZenBase, double ZenLinear,
    double ZenQuadratic);
public sealed record MonsterRank(string Id, double Hp, double Damage, double Armor, double AttackSeconds,
    double Xp, double Zen, bool Telegraph);
public sealed record ProgressionRules(double[][] NormalSeconds, double[][] MasterSeconds,
    double OverlevelGraceRanks, double OverlevelDecayRanks, double OverlevelFloor,
    double UnderlevelRewardCap, double DeathXpLoss, double PartyMonsterHpPerExtra,
    double PartyMonsterDamagePerExtra, double PartyXpPerExtra, int PartyMaxMembers,
    Dictionary<string, int> MasterAllocation);
public sealed record LootRules(double MoneyChance, double JewelChance, double SmartClassChance,
    int ExcellentPityKills, int RareEligibilityRank, LootEntry[] Equipment, int BossTokens,
    int EliteTokens, int ExcellentTokenCost, int AncientTokenCost, int SocketTokenCost,
    double BossTokenCooldownMinutes, int EventTokenCapPerDay);
public sealed record LootEntry(string Id, double Chance);
public sealed record EconomyRules(long StartingZen, long WalletCap, double VendorBuybackFraction,
    double RepairZenPerCombatMinuteRank, double TravelZenPerRank, int RespecFreeThroughLevel,
    double RespecZenPerRank, double HealthPotionCooldown, double ManaPotionCooldown,
    double SdPotionCooldown, double PlannedCraftSpendFraction, double SaleValuePerNormalMonsterZen,
    Potion[] Potions);
public sealed record Potion(string Id, int Group, int Number, string Kind, int Unlock,
    double Fraction, double Cap, long Price);
public sealed record EnhancementRules(int MaxLevel, double StatGainPerLevel, double ZenFeeBase,
    double ZenFeeSquared, UpgradeStep[] Steps);
public sealed record UpgradeStep(int Level, double Chance, int Pity, int Jewels);
public sealed record EventSpec(string Id, int DurationMinutes, int RewardKillEquivalents,
    int Tokens, int RepeatMinutes);
public sealed record PvpRules(double DamageMultiplier, double ShieldToHealthRatio, double ShieldDamageShare,
    double ControlDurationCapSeconds, double ControlImmunitySeconds, double HealingMultiplier,
    double LifeStealMultiplier, int MatchLevel, int MatchMasterLevel, double GearFactorCap,
    double ReferenceHealth, double AllocatedHealthWeight);
public sealed record Route(string Id, int[] MapIds, double MinRank, double MaxRank, bool Master);
