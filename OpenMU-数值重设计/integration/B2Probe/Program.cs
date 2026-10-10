using System.Text;
using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.Persistence.InMemory;
using MUnique.OpenMU.Persistence.Initialization;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix;

// B2 v3: REAL per-level naked base damage table (for B1) + PVP mirror TTK quantification.

var outDir = Path.Combine(AppContext.BaseDirectory, "artifacts", "r4");
Directory.CreateDirectory(outDir);
var ExtraLookup = new Dictionary<string, List<MUnique.OpenMU.AttributeSystem.ConstValueAttribute>>();

var provider = new InMemoryPersistenceContextProvider();
using var soloCtx = provider.CreateNewConfigurationContext();
var soloCfg = soloCtx.CreateNew<GameConfiguration>();
new GameConfigurationInitializer(soloCtx, soloCfg).Initialize();
new SoloBalanceInitializer(soloCtx, soloCfg).Initialize();

// unlock-class patch (same as before)
void Patch(string name, bool caster)
{
    var list = new List<MUnique.OpenMU.AttributeSystem.ConstValueAttribute>();
    if (caster) { list.Add(new(10, Stats.MinimumWizBaseDmg)); list.Add(new(15, Stats.MaximumWizBaseDmg)); }
    else { list.Add(new(10, Stats.MinimumPhysBaseDmg)); list.Add(new(15, Stats.MaximumPhysBaseDmg)); }
    list.Add(new(70, Stats.AttackRatePvm));
    list.Add(new(95, Stats.DefenseRatePvm));
    ExtraLookup[name] = list;
}
Patch("Magic Gladiator", false); Patch("Dark Lord", false); Patch("Rage Fighter", false); Patch("Summoner", true);

var starters = soloCfg.CharacterClasses.Where(c => c.CanGetCreated && !c.IsMasterClass).OrderBy(c => c.Number).ToList();
var levels = new[] { 1, 10, 15, 40, 80, 120, 148 };

// ---- 1. REAL naked base damage table ----
var tbl = new StringBuilder();
tbl.AppendLine("class\tlevel\tstr\tagi\tvit\tene\tbaseMin\tbaseMax\tavgDmg\tskillX2\tatkRatePvm\tdefRatePvm\tmaxHp\tdefPvp");
foreach (var cls in starters)
{
    foreach (var L in levels)
    {
        var sys = BuildSystem(cls);
        Allocate(cls, sys, L);
        double bMin = BaseDmg(cls, sys, true), bMax = BaseDmg(cls, sys, false);
        double avg = (bMin + bMax) / 2.0;
        tbl.AppendLine($"{cls.Name}\t{L}\t{sys[Stats.TotalStrength]:F0}\t{sys[Stats.TotalAgility]:F0}\t{sys[Stats.TotalVitality]:F0}\t{sys[Stats.TotalEnergy]:F0}\t{bMin:F1}\t{bMax:F1}\t{avg:F1}\t{avg*2:F1}\t{sys[Stats.AttackRatePvm]:F1}\t{sys[Stats.DefenseRatePvm]:F1}\t{sys[Stats.MaximumHealth]:F1}\t{sys[Stats.DefensePvp]:F1}");
    }
}
File.WriteAllText(Path.Combine(outDir, "real-damage-table.tsv"), tbl.ToString());
Console.WriteLine("=== REAL naked base damage (avg = per-hit vs def0; skillX2 = starter skill) ===");
foreach (var cls in starters)
{
    Console.Write($"{cls.Name,-13} ");
    foreach (var L in levels)
    {
        var sys = BuildSystem(cls); Allocate(cls, sys, L);
        double avg = (BaseDmg(cls, sys, true) + BaseDmg(cls, sys, false)) / 2.0;
        Console.Write($"L{L}={avg,6:F0} ");
    }
    Console.WriteLine();
}

// ---- 2. PVP mirror TTK: DK attacks DW, DW attacks DK, naked ----
Console.WriteLine();
Console.WriteLine("=== PVP mirror (naked, legacy: dmg=base-defPvp; no EGB floor) ===");
var pvp = new StringBuilder();
pvp.AppendLine("level\tDK->DW_dmg\tDK->DW_hits\tDW->DK_dmg\tDW->DK_hits");
foreach (var L in levels)
{
    var dk = BuildSystem(starters.First(c => c.Name.ToString()=="Dark Knight")); Allocate(starters.First(c=>c.Name.ToString()=="Dark Knight"), dk, L);
    var dw = BuildSystem(starters.First(c => c.Name.ToString()=="Dark Wizard")); Allocate(starters.First(c=>c.Name.ToString()=="Dark Wizard"), dw, L);
    // DK attacks DW: DK basePhys - DW defPvp
    double dkBase = (dk[Stats.MinimumPhysBaseDmg] + dk[Stats.MaximumPhysBaseDmg]) / 2.0;
    double dkDmg = Math.Max(1, dkBase - dw[Stats.DefensePvp]);
    double dwHp = dw[Stats.MaximumHealth];
    double dwBase = (dw[Stats.MinimumWizBaseDmg] + dw[Stats.MaximumWizBaseDmg]) / 2.0;
    double dwDmg = Math.Max(1, dwBase - dk[Stats.DefensePvp]);
    double dkHp = dk[Stats.MaximumHealth];
    pvp.AppendLine($"{L}\t{dkDmg:F1}\t{dwHp/dkDmg:F1}\t{dwDmg:F1}\t{dkHp/dwDmg:F1}");
    Console.WriteLine($"L{L,-4} DK->DW {dkDmg,7:F0}/hit => {dwHp/dkDmg,5:F1} hits to kill DW({dwHp:F0}hp) | DW->DK {dwDmg,7:F0}/hit => {dkHp/dwDmg,5:F1} hits to kill DK({dkHp:F0}hp)");
}
File.WriteAllText(Path.Combine(outDir, "real-pvp-mirror.tsv"), pvp.ToString());
Console.WriteLine("Outputs in " + outDir);
return 0;

AttributeSystem BuildSystem(CharacterClass cls)
{
    var stats = cls.StatAttributes.Where(sa => sa.Attribute is not null)
        .Select(sa => new StatAttribute(sa.Attribute!, sa.BaseValue)).ToList();
    var bases = cls.BaseAttributeValues.ToList();
    if (ExtraLookup.TryGetValue(cls.Name.ToString(), out var extra)) bases.AddRange(extra);
    return new AttributeSystem(stats, bases, cls.AttributeCombinations.ToList());
}
bool IsCaster(CharacterClass cls) => cls.Name.ToString() is "Dark Wizard" or "Summoner";
double BaseDmg(CharacterClass cls, AttributeSystem sys, bool min) => IsCaster(cls)
    ? (min ? sys[Stats.MinimumWizBaseDmg] : sys[Stats.MaximumWizBaseDmg])
    : (min ? sys[Stats.MinimumPhysBaseDmg] : sys[Stats.MaximumPhysBaseDmg]);
void Allocate(CharacterClass cls, AttributeSystem sys, int level)
{
    double extra = (level - 1) * 8.0;
    double str = sys[Stats.TotalStrength], agi = sys[Stats.TotalAgility], vit = sys[Stats.TotalVitality], ene = sys[Stats.TotalEnergy];
    switch (cls.Name.ToString())
    {
        case "Dark Knight": str += extra * 0.5; vit += extra * 0.3; agi += extra * 0.2; break;
        case "Dark Wizard": ene += extra * 0.8; vit += extra * 0.2; break;
        case "Fairy Elf": agi += extra * 0.7; vit += extra * 0.2; str += extra * 0.1; break;
        case "Magic Gladiator": str += extra * 0.5; ene += extra * 0.3; vit += extra * 0.2; break;
        case "Dark Lord": str += extra * 0.5; vit += extra * 0.3; ene += extra * 0.2; break;
        case "Summoner": ene += extra * 0.8; vit += extra * 0.2; break;
        case "Rage Fighter": str += extra * 0.6; vit += extra * 0.2; ene += extra * 0.2; break;
    }
    sys[Stats.Level] = (float)level;
    sys[Stats.BaseStrength] = (float)str;
    sys[Stats.BaseAgility] = (float)agi;
    sys[Stats.BaseVitality] = (float)vit;
    sys[Stats.BaseEnergy] = (float)ene;
}