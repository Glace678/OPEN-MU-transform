using System.Globalization;
using System.Text;
using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.Persistence.InMemory;
using MUnique.OpenMU.Persistence.Initialization;
using MUnique.OpenMU.Persistence.Initialization.Updates;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix;

// INDEPENDENT auditor probe R4 (v2) — applies the REAL shipping order on fresh S6:
//   seed (GameConfigurationInitializer) -> SoloBalanceInitializer (compress) -> v110 SoloCombatBalanceSeason6UpdatePlugIn.
// Reads real attributes, computes hit/damage/floor/overrate TTK with the auditor's OWN legacy replication.
var inv = CultureInfo.InvariantCulture;
var outDir = Path.Combine(AppContext.BaseDirectory, "artifacts", "r4");
Directory.CreateDirectory(outDir);

var provider = new InMemoryPersistenceContextProvider();
var cfgCtx = provider.CreateNewConfigurationContext();
var cfg = cfgCtx.CreateNew<GameConfiguration>();
new GameConfigurationInitializer(cfgCtx, cfg).Initialize();
new SoloBalanceInitializer(cfgCtx, cfg).Initialize();            // startup :510
using (var runtimeCtx = provider.CreateNewContext(cfg))
{
    new SoloCombatBalanceSeason6UpdatePlugIn().ApplyUpdateAsync(runtimeCtx, cfg).AsTask().GetAwaiter().GetResult();  // startup :531
}

float R(MonsterDefinition m, AttributeDefinition a) => m.Attributes.FirstOrDefault(x => x.AttributeDefinition == a)?.Value ?? 0f;
const double PointsPerLevel = 11.0;
AttributeSystem BuildSys(CharacterClass c)
{
    var stats = c.StatAttributes.Where(sa => sa.Attribute is not null).Select(sa => new StatAttribute(sa.Attribute!, sa.BaseValue)).ToList();
    var bases = c.BaseAttributeValues.ToList();
    return new AttributeSystem(stats, bases, c.AttributeCombinations.ToList());
}
void Allocate(AttributeSystem sys, string name, int level)
{
    double extra = (level - 1) * PointsPerLevel;
    double str = sys[Stats.BaseStrength], agi = sys[Stats.BaseAgility], vit = sys[Stats.BaseVitality], ene = sys[Stats.BaseEnergy];
    switch (name)
    {
        case "Dark Knight": str += extra*0.5; vit += extra*0.3; agi += extra*0.2; break;
        case "Dark Wizard": ene += extra*0.8; vit += extra*0.2; break;
        case "Fairy Elf": agi += extra*0.7; vit += extra*0.2; str += extra*0.1; break;
        case "Magic Gladiator": str += extra*0.5; ene += extra*0.3; vit += extra*0.2; break;
        case "Dark Lord": str += extra*0.5; vit += extra*0.3; ene += extra*0.2; break;
        case "Summoner": ene += extra*0.8; vit += extra*0.2; break;
        case "Rage Fighter": str += extra*0.6; vit += extra*0.2; ene += extra*0.2; break;
    }
    sys[Stats.Level]=level; sys[Stats.BaseStrength]=(float)str; sys[Stats.BaseAgility]=(float)agi; sys[Stats.BaseVitality]=(float)vit; sys[Stats.BaseEnergy]=(float)ene;
}
bool Caster(string n) => n is "Dark Wizard" or "Summoner";
double BaseDmg(AttributeSystem sys, string n, bool min) => Caster(n)
    ? (min?sys[Stats.MinimumWizBaseDmg]:sys[Stats.MaximumWizBaseDmg])
    : (min?sys[Stats.MinimumPhysBaseDmg]:sys[Stats.MaximumPhysBaseDmg]);
(double ttk,double hits,double hit,double eff) Ttk(string cls, AttributeSystem sys, MonsterDefinition mon, int level)
{
    double atkRate=sys[Stats.AttackRatePvm];
    double bMin=BaseDmg(sys,cls,true), bMax=BaseDmg(sys,cls,false);
    double hp=R(mon,Stats.MaximumHealth), def=R(mon,Stats.DefenseBase), monDr=R(mon,Stats.DefenseRatePvm);
    double legacy = monDr<atkRate ? 1.0-monDr/atkRate : 0.03;
    double hit = level<=20 ? Math.Max(legacy,0.80):legacy;
    double avg=(bMin+bMax)/2; double dmg=avg-def;
    if(level>40 && monDr>atkRate) dmg*=0.3;
    double floor=Math.Max(4.0,level/10.0); if(dmg<floor)dmg=floor;
    double hits=hp/Math.Max(1.0,dmg);
    return (hits/hit,hits,hit,dmg);
}
var starters = cfg.CharacterClasses.Where(c=>c.CanGetCreated && !c.IsMasterClass).OrderBy(c=>c.Number).ToList();
var sb=new StringBuilder(); void Line(string s){Console.WriteLine(s);sb.AppendLine(s);}

Line("### A: DELIVERED (seed->solo->v110) monster stats vs embedded target (Post) ###");
Line("num\tname\tdlvHP\ttgtPostHP\tdlvDef\ttgtPostDef\tdlvDr\ttgtPostDr");
double[,] tgt = {{3,162,5,24},{26,188,5,26},{418,210,6,27},{28,360,10,34},{29,456,13,38},{30,524,14,40},{12,1474,26,26}};
foreach(var n in new short[]{3,26,418,28,29,30,12}){
    var m=cfg.Monsters.FirstOrDefault(x=>x.Number==n); if(m==null)continue;
    Line($"{m.Number}\t{m.Designation}\t{R(m,Stats.MaximumHealth):F1}\t?\t{R(m,Stats.DefenseBase):F1}\t?\t{R(m,Stats.DefenseRatePvm):F1}\t?");
}

Line("");
Line("### B: root-cause monsters DELIVERED state, real attacker at monster level ###");
Line("num\tname\tmLvl\tclass\thits\thit\teff\tttk_s");
foreach(var n in new short[]{3,26,28,29,30,12}){
    var m=cfg.Monsters.FirstOrDefault(x=>x.Number==n); if(m==null)continue;
    int ml=(int)R(m,Stats.Level);
    foreach(var cn in new[]{"Dark Knight","Dark Wizard","Fairy Elf"}){
        var c=starters.First(x=>x.Name.ToString()==cn);
        var sys=BuildSys(c); Allocate(sys,cn,ml);
        var r=Ttk(cn,sys,m,ml);
        Line($"{m.Number}\t{m.Designation}\t{ml}\t{cn}\t{r.hits:F1}\t{r.hit:F2}\t{r.eff:F1}\t{r.ttk:F1}");
    }
}

Line("");
Line("### C: real naked base damage at level (delivered player) ###");
Line("class\tL10\tL40\tL80\tL120\tL148");
foreach(var cn in new[]{"Dark Knight","Dark Wizard","Fairy Elf"}){
    var c=starters.First(x=>x.Name.ToString()==cn);
    var parts=new List<string>();
    foreach(var L in new[]{10,40,80,120,148}){ var s=BuildSys(c); Allocate(s,cn,L); parts.Add(((BaseDmg(s,cn,true)+BaseDmg(s,cn,false))/2).ToString("F0")); }
    Line($"{cn}\t{string.Join("\t",parts)}");
}

Line("");
Line("### D: DELIVERED TTK per level band (normal monsters) auditor model ###");
Line("band\tclass\tn\tmedianTTK\tmin\tmax");
var bands=new (int lo,int hi,string lab)[]{(1,15,"newbie1-15"),(16,40,"early16-40"),(41,80,"mid41-80"),(81,120,"late81-120"),(121,148,"end121-148")};
var normals=cfg.Monsters.Where(m=>m.ObjectKind==NpcObjectKind.Monster).ToList();
foreach(var (lo,hi,lab) in bands){
    var pool=normals.Where(m=>{var l=R(m,Stats.Level);return l>=lo&&l<=hi;}).ToList();
    int aLvl=(lo+hi)/2;
    foreach(var cn in new[]{"Dark Knight","Dark Wizard","Fairy Elf"}){
        var c=starters.First(x=>x.Name.ToString()==cn);
        var tt=new List<double>();
        foreach(var m in pool){ var s=BuildSys(c); Allocate(s,cn,aLvl); tt.Add(Ttk(cn,s,m,aLvl).ttk); }
        if(tt.Count==0)continue; tt.Sort();
        Line($"{lab}\t{cn}\t{tt.Count}\t{tt[tt.Count/2]:F1}\t{tt[0]:F1}\t{tt[^1]:F1}");
    }
}

Line("");
Line("### E: MG/DL/RF/SUM delivered consts, naked L1 vs Beetle(28) ###");
Line("class\tbMin\tbMax\tatkRate\thits\tttk_s");
foreach(var cn in new[]{"Magic Gladiator","Dark Lord","Summoner","Rage Fighter"}){
    var c=starters.FirstOrDefault(x=>x.Name.ToString()==cn); if(c==null){Line($"{cn}\t<missing>");continue;}
    var m=cfg.Monsters.First(x=>x.Number==28);
    var s=BuildSys(c); Allocate(s,cn,1);
    var r=Ttk(cn,s,m,1);
    Line($"{cn}\t{BaseDmg(s,cn,true):F1}\t{BaseDmg(s,cn,false):F1}\t{s[Stats.AttackRatePvm]:F0}\t{r.hits:F1}\t{r.ttk:F1}");
}

File.WriteAllText(Path.Combine(outDir,"audit-delivered-ttk.tsv"), sb.ToString(), new UTF8Encoding(false));
Console.WriteLine("done");