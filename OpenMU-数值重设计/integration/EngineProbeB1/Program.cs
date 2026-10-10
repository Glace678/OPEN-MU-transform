using System.Globalization;
using System.Text;
using System.Text.Json;
using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.Persistence.InMemory;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix;

// ============================================================================
// B1 probe v2 (REBUILT per cross-audit): uses B2's REAL AttributeSystem per-level
// naked base damage (DK/DW/FE), NOT a hand-written curve. Designs post-solo HP so
// the MEDIAN class lands on target TTK bands; def/rate/atk keep the self-consistent
// design. Legacy path replicated exactly. Read-only on seed; writes artifacts only.
// ============================================================================
if (args.Length != 1) { Console.Error.WriteLine("Usage: EngineProbeB1 <out-dir>"); return 2; }
var outDir = args[0]; Directory.CreateDirectory(outDir);
var inv = CultureInfo.InvariantCulture;

var provider = new InMemoryPersistenceContextProvider();
using var ctx = provider.CreateNewConfigurationContext();
var cfg = ctx.CreateNew<GameConfiguration>();
new GameConfigurationInitializer(ctx, cfg).Initialize();

// ---------- REAL per-class naked base-mid damage (B2 relationships, audit-verified) ----------
// DK: min=10+0.7Str max=15+1.05Str, 11pt/lv (50%Str/30%Vit/20%Agi)
static double DkB(double L) => Interp(new double[]{1,10,40,80,120,148}, new double[]{37.0,80,225,417,610,744}, L);
// DW: min=10+0.6Ene max=15+1.0Ene, 80% Ene
static double DWb(double L) => Interp(new double[]{1,10,40,80,120,148}, new double[]{36.5,100,311,593,874,1071}, L);
// FE(bow): min=10+0.5Agi+0.25Str max=15+0.85Agi+0.45Str, 70% Agi
static double FEb(double L) => Interp(new double[]{1,10,40,80,120,148}, new double[]{41.85,91,256,476,696,850}, L);
static double Median(double a,double b,double c){ var x=new[]{a,b,c}; Array.Sort(x); return x[1]; }

// representative player atkRate (DK: 70+5Lv+1.5Agi+0.25Str) and HP (DK maxHp=35+2Lv+3Vit)
static double PlayerAtkRate(double L) => 102.3 + 9.675*L;
static double PlayerHp(double L) => 100.1 + 11.9*L;

const double SwingSec = 1.0;
static double TargetSwings(double L) => L<=15 ? 5.0 : L<=40 ? 7.0 : L<=80 ? 10.0 : L<=120 ? 15.0 : 20.0;
static double HitChance(double L) => L<=20 ? 0.80 : 0.90;

// solo Compress + exact inverse
static double Compress(double v, double mult, double knee, double max){ if(v<=0)return v; double s=v*mult; double f=s<=knee?s:knee+Math.Sqrt((s-knee)*knee); return Math.Clamp(f,1,max); }
static double CompressInv(double c, double mult, double knee, double max){ c=Math.Clamp(c,1,max); double s=c<=knee?c:knee+(c-knee)*(c-knee)/knee; return s/mult; }

// map -> monsters
var monToMaps = new Dictionary<short, List<string>>();
foreach (var map in cfg.Maps)
  foreach (var sp in map.MonsterSpawns)
    if (sp.MonsterDefinition is not null){ if(!monToMaps.TryGetValue(sp.MonsterDefinition.Number,out var l)){l=new();monToMaps[sp.MonsterDefinition.Number]=l;} l.Add($"{map.Number}"); }

var monList = cfg.Monsters.Where(m=>m.ObjectKind==MUnique.OpenMU.DataModel.Configuration.NpcObjectKind.Monster).ToList();
var bucketMedian = monList.GroupBy(m=>(int)(Read(m,Stats.Level)/5)).ToDictionary(g=>g.Key, g=>{ var hs=g.Select(x=>(double)Read(x,Stats.MaximumHealth)).OrderBy(y=>y).ToList(); return hs[hs.Count/2]; });
static double BucketMed(Dictionary<int,double> d,int b)=> d.TryGetValue(b,out double v)?v: d.OrderBy(x=>Math.Abs(x.Key-b)).First().Value;

var results = new List<Row>();
foreach (var m in monList.OrderBy(m=>m.Number))
{
  double L = Read(m, Stats.Level); if (L<1) L=1;
  double stockHp = Read(m, Stats.MaximumHealth);
  double medStock = BucketMed(bucketMedian,(int)(L/5));
  double ratio = medStock>0 ? stockHp/medStock : 1.0;
  string rank = ratio>=2.6 ? "boss" : ratio>=1.45 ? "elite" : "normal";
  double rankMult = rank=="boss"?5.0 : rank=="elite"?2.2 : 1.0;
  string maps = monToMaps.TryGetValue(m.Number, out var l) ? string.Join("/", l.Distinct()) : "event/none";

  // per-class naked base at this monster's level (player levels to monster level)
  double dk=DkB(L), dw=DWb(L), fe=FEb(L);
  double medBase = Median(dk,dw,fe);
  double postDef = Math.Max(2.0, Math.Round(0.10*medBase));
  // per-class landed eff after def
  double effDk=Math.Max(dk-postDef, Math.Max(4.0,L/10.0));
  double effDw=Math.Max(dw-postDef, Math.Max(4.0,L/10.0));
  double effFe=Math.Max(fe-postDef, Math.Max(4.0,L/10.0));
  double medEff = Median(effDk,effDw,effFe);
  double hc = HitChance(L);
  double S = TargetSwings(L)*rankMult;
  double postHp = Math.Round(medEff*hc*S);

  double postDefRate = Math.Max(1.0, Math.Round((1-hc)*PlayerAtkRate(L)));
  double postMonAtkRate = Math.Round(25 + 4.0*L);
  double php = PlayerHp(L);
  double postMinAtk = Math.Max(1.0, Math.Round(0.09*php));
  double postMaxAtk = Math.Max(postMinAtk+1, Math.Round(0.14*php));

  double preHp = Math.Round(CompressInv(postHp,0.45,20000,300000));
  double preDef = Math.Round(CompressInv(postDef,0.45,300,1000));
  double preDefRate = Math.Round(CompressInv(postDefRate,0.45,300,1000));
  double preAtkRate = Math.Round(CompressInv(postMonAtkRate,0.70,1500,4000));
  double preMinAtk = Math.Round(CompressInv(postMinAtk,0.40,400,1500));
  double preMaxAtk = Math.Round(CompressInv(postMaxAtk,0.40,400,1500));

  // 3-class TTK verification
  double ttkDk = postHp/(effDk*hc*SwingSec);
  double ttkDw = postHp/(effDw*hc*SwingSec);
  double ttkFe = postHp/(effFe*hc*SwingSec);

  results.Add(new Row{ num=m.Number, name=m.Designation.ToString()??"", rank=rank, level=(int)L, maps=maps,
    postHp=(int)postHp, preHp=(int)preHp, postMinAtk=(int)postMinAtk, postMaxAtk=(int)postMaxAtk, preMinAtk=(int)preMinAtk, preMaxAtk=(int)preMaxAtk,
    postDef=(int)postDef, preDef=(int)preDef, postDefRate=(int)postDefRate, preDefRate=(int)preDefRate, postAtkRate=(int)postMonAtkRate, preAtkRate=(int)preAtkRate,
    ttkDK=Math.Round(ttkDk,1), ttkDW=Math.Round(ttkDw,1), ttkFE=Math.Round(ttkFe,1) });
}

// write TSV
var sb=new StringBuilder();
sb.AppendLine("map\tnum\tname\trank\tlevel\tpreHp\tpostHp\tpreMinAtk\tpostMinAtk\tpreMaxAtk\tpostMaxAtk\tpreDefBase\tpostDefBase\tpreDefRatePvm\tpostDefRatePvm\tpreAtkRatePvm\tpostAtkRatePvm\tttkDKs\tttkDWs\tttkFEs");
foreach(var r in results) sb.AppendLine(string.Join("\t", r.maps,r.num,r.name,r.rank,r.level,r.preHp,r.postHp,r.preMinAtk,r.postMinAtk,r.preMaxAtk,r.postMaxAtk,r.preDef,r.postDef,r.preDefRate,r.postDefRate,r.preAtkRate,r.postAtkRate,r.ttkDK.ToString(inv),r.ttkDW.ToString(inv),r.ttkFE.ToString(inv)));
File.WriteAllText(Path.Combine(outDir,"monster-targets.tsv"), sb.ToString());
var opts=new JsonSerializerOptions{WriteIndented=true};
File.WriteAllText(Path.Combine(outDir,"monster-targets.json"), JsonSerializer.Serialize(results, opts));

Console.WriteLine($"total designed: {results.Count} (normal={results.Count(r=>r.rank=="normal")} elite={results.Count(r=>r.rank=="elite")} boss={results.Count(r=>r.rank=="boss")})");
Console.WriteLine();
Console.WriteLine("seg   lvRange   n    postHpMed   TTK-DK   TTK-DW   TTK-FE   (band)");
foreach(var seg in new[]{(1,15,"newbie 4-8"),(16,40,"early 5-10"),(41,80,"mid 6-15"),(81,120,"late 10-20"),(121,200,"end 12-25")}){
  var g=results.Where(r=>r.level>=seg.Item1 && r.level<=seg.Item2 && r.rank=="normal").ToList();
  if(g.Count==0) continue;
  double h=g.Select(r=>r.postHp).OrderBy(x=>x).ElementAt(g.Count/2);
  double dk=g.Average(r=>r.ttkDK), dw=g.Average(r=>r.ttkDW), fe=g.Average(r=>r.ttkFE);
  Console.WriteLine($"      {seg.Item1,3}-{seg.Item2,3}  {g.Count,3}  {h,9:F0}   {dk,5:F1}   {dw,5:F1}   {fe,5:F1}   {seg.Item3}");
}
Console.WriteLine();
foreach(var n in new short[]{3,26,28,29,30,12}){ var r=results.FirstOrDefault(x=>x.num==n); if(r!=null)
  Console.WriteLine($"#{r.num,-4}{r.name,-16} lv{r.level,3} {r.rank,-6} postHP={r.postHp,6} def={r.postDef,4} | TTK DK{r.ttkDK} DW{r.ttkDW} FE{r.ttkFE}s"); }
Console.WriteLine();
double mn = results.Where(r=>r.rank=="normal").Min(r=>new[]{r.ttkDK,r.ttkDW,r.ttkFE}.Min());
double mx = results.Where(r=>r.rank=="normal").Max(r=>new[]{r.ttkDK,r.ttkDW,r.ttkFE}.Max());
Console.WriteLine($"normal per-class TTK range: min={mn:F1}s max={mx:F1}s");
Console.WriteLine("wrote monster-targets.tsv/.json");
return 0;

static float Read(MonsterDefinition m, AttributeDefinition s) => m.Attributes.FirstOrDefault(a=>a.AttributeDefinition==s)?.Value ?? 0f;
static double Interp(double[] xs, double[] ys, double x){ if(x<=xs[0])return ys[0]; if(x>=xs[^1])return ys[^1]; for(int i=0;i<xs.Length-1;i++) if(x<=xs[i+1]){ double t=(x-xs[i])/(xs[i+1]-xs[i]); return ys[i]+t*(ys[i+1]-ys[i]); } return ys[^1]; }
internal class Row { public short num; public string name=""; public string rank=""; public int level; public string maps="";
  public int preHp,postHp,preMinAtk,postMinAtk,preMaxAtk,postMaxAtk,preDef,postDef,preDefRate,postDefRate,preAtkRate,postAtkRate;
  public double ttkDK,ttkDW,ttkFE;
  public short Num{get=>num;set=>num=value;} public string Name{get=>name;set=>name=value;}
  public string Rank{get=>rank;set=>rank=value;} public int Level{get=>level;set=>level=value;} public string Map{get=>maps;set=>maps=value;}
  public int PreHp{get=>preHp;set=>preHp=value;} public int PostHp{get=>postHp;set=>postHp=value;}
  public int PreMinAtk{get=>preMinAtk;set=>preMinAtk=value;} public int PostMinAtk{get=>postMinAtk;set=>postMinAtk=value;}
  public int PreMaxAtk{get=>preMaxAtk;set=>preMaxAtk=value;} public int PostMaxAtk{get=>postMaxAtk;set=>postMaxAtk=value;}
  public int PreDefBase{get=>preDef;set=>preDef=value;} public int PostDefBase{get=>postDef;set=>postDef=value;}
  public int PreDefRatePvm{get=>preDefRate;set=>preDefRate=value;} public int PostDefRatePvm{get=>postDefRate;set=>postDefRate=value;}
  public int PreAtkRatePvm{get=>preAtkRate;set=>preAtkRate=value;} public int PostAtkRatePvm{get=>postAtkRate;set=>postAtkRate=value;}
  public double TtkDKs{get=>ttkDK;set=>ttkDK=value;} public double TtkDWs{get=>ttkDW;set=>ttkDW=value;} public double TtkFEs{get=>ttkFE;set=>ttkFE=value;}
}