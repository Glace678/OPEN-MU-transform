using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.Persistence.InMemory;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix;
using MUnique.OpenMU.Persistence.Initialization;

if (args.Length != 1) { Console.Error.WriteLine("Usage: EngineProbe <output-dir>"); return 2; }
var output = ResolveControlledOutput(args[0]);
Directory.CreateDirectory(output);

var provider = new InMemoryPersistenceContextProvider();
using var stockCtx = provider.CreateNewConfigurationContext();
var stockCfg = stockCtx.CreateNew<GameConfiguration>();
new GameConfigurationInitializer(stockCtx, stockCfg).Initialize();

using var soloCtx = provider.CreateNewConfigurationContext();
var soloCfg = soloCtx.CreateNew<GameConfiguration>();
new GameConfigurationInitializer(soloCtx, soloCfg).Initialize();
new SoloBalanceInitializer(soloCtx, soloCfg).Initialize();

Console.WriteLine($"solo marker installed -> stock:{SoloBalance.IsEnabled(stockCfg)} solo:{SoloBalance.IsEnabled(soloCfg)}");
Console.WriteLine($"ExperienceRate stock={stockCfg.ExperienceRate} solo={soloCfg.ExperienceRate}");
Sanity(stockCfg, "stock"); Sanity(soloCfg, "solo");

var starters = stockCfg.CharacterClasses.Where(c => c.CanGetCreated && !c.IsMasterClass).OrderBy(c=>c.Number).Take(3).ToList();
Console.WriteLine("starter classes: " + string.Join(", ", starters.Select(c=>c.Name.ToString())));
short[] monIds = [3,26,27,28,29,30];

var report = new List<object>();
foreach (var (cfg, tag) in new[]{ (stockCfg,"STOCK-live"), (soloCfg,"SOLO-applied") })
{
  foreach (var cls in starters)
  {
    foreach (var mid in monIds)
    {
      var mon = cfg.Monsters.FirstOrDefault(m => m.Number == mid);
      if (mon is null) continue;
      report.Add(RunOne(cfg, cls, mon, tag));
    }
  }
}
var sb = new System.Text.StringBuilder();
sb.AppendLine("tag`tcls`tmon`tmonNum`thp`tdef`tdefrate`tlevel`tatkRate`twiz`tbaseMin`tbaseMax`thit`teffMin`teffMax`texpHits`texpSwings`ttk1s");
foreach (dynamic r in report) sb.AppendLine($"{r.tag}`t{r.cls}`t{r.mon}`t{r.monNum}`t{r.monHp}`t{r.monDef}`t{r.monDefRate}`t{r.level}`t{r.atkRate}`t{r.wiz}`t{r.baseMin}`t{r.baseMax}`t{r.hit}`t{r.effMin}`t{r.effMax}`t{r.expHits}`t{r.expSwings}`t{r.ttk1s}");
File.WriteAllText(Path.Combine(output,"ttk-report.tsv"), sb.ToString());

Console.WriteLine();
Console.WriteLine("tag          cls          monster            hp   def  baseMin-baseMax  hit%   effMin-effMax  expHits  ttk(s@1s/swing)");
foreach (dynamic r in report)
{
  Console.WriteLine($"{r.tag,-11} {r.cls,-12} {r.mon,-16} {r.monHp,5:F0} {r.monDef,4:F0}  {r.baseMin,5:F1}-{r.baseMax,-5:F1}   {r.hit,4:P0}  {r.effMin,4:F0}-{r.effMax,-4:F0}     {r.expHits,6:F1}  {r.ttk1s,7:F1}");
}
Console.WriteLine("No save, db connection, network listener or config write was performed.");
return 0;

static void Sanity(GameConfiguration cfg, string tag)
{
  foreach (var n in new short[]{3,26,12})
  {
    var m = cfg.Monsters.First(x=>x.Number==n);
    Console.WriteLine($"[{tag}] #{n} {m.Designation} HP={Read(m,Stats.MaximumHealth):F0} def={Read(m,Stats.DefenseBase):F0} defrate={Read(m,Stats.DefenseRatePvm):F0} atkrate={Read(m,Stats.AttackRatePvm):F0}");
  }
}

static object RunOne(GameConfiguration cfg, CharacterClass cls, MonsterDefinition mon, string tag)
{
  var sys = BuildPlayerSystem(cls);
  double level = sys[Stats.Level];
  double atkRate = sys[Stats.AttackRatePvm];
  bool wiz = sys[Stats.MinimumWizBaseDmg] > 0;
  double baseMin = wiz ? sys[Stats.MinimumWizBaseDmg] : sys[Stats.MinimumPhysBaseDmg];
  double baseMax = wiz ? sys[Stats.MaximumWizBaseDmg] : sys[Stats.MaximumPhysBaseDmg];

  double monHp = Read(mon, Stats.MaximumHealth);
  double monDef = Read(mon, Stats.DefenseBase);
  double monDefRate = Read(mon, Stats.DefenseRatePvm);

  // LIVE legacy path (no BalanceV1 marker on this DB)
  double legacyHit = monDefRate < atkRate ? 1.0 - monDefRate / atkRate : 0.03;
  double hit = EarlyGameBalance.AdjustHitChance((int)level, (float)legacyHit);

  double floor = Math.Max(4.0, level / 10.0);
  double effMin = Math.Max(baseMin - monDef, floor);
  double effMax = Math.Max(baseMax - monDef, floor);
  double avg = (effMin + effMax) / 2.0;
  double expHits = monHp / Math.Max(1.0, avg);
  double expSwings = expHits / hit;
  return new { tag, cls=cls.Name.ToString(), mon=mon.Designation.ToString(), monNum=mon.Number,
    monHp, monDef, monDefRate, level, atkRate, wiz, baseMin, baseMax,
    legacyHit, hit, effMin, effMax, avg, expHits, expSwings, ttk1s=expSwings*1.0 };
}

static float Read(MonsterDefinition monster, AttributeDefinition stat) =>
  monster.Attributes.FirstOrDefault(a => a.AttributeDefinition == stat)?.Value ?? 0;

static AttributeSystem BuildPlayerSystem(CharacterClass cls)
{
  var statAttributes = cls.StatAttributes.Where(sa=>sa.Attribute is not null)
    .Select(sa=>new StatAttribute(sa.Attribute!, sa.BaseValue)).ToList();
  var baseAttributes = cls.BaseAttributeValues.ToList();
  var relationships = cls.AttributeCombinations.ToList();
  return new AttributeSystem(statAttributes, baseAttributes, relationships);
}

static string ResolveControlledOutput(string requested)
{
  var allowedRoot = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "artifacts"));
  var output = Path.GetFullPath(requested);
  bool within = output.StartsWith(allowedRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                || string.Equals(output, allowedRoot, StringComparison.OrdinalIgnoreCase);
  if (!within) { Console.Error.WriteLine($"Refusing to write outside controlled artifacts root: {output}"); Environment.Exit(2); }
  return output;
}