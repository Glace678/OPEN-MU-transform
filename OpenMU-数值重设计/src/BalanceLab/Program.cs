using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OpenMu.Balance;

CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
if (args.Length < 2 || args[0] is not ("verify" or "report"))
{
    Console.Error.WriteLine("Usage: BalanceLab verify|report <design.json> [output-directory]");
    return 2;
}
var designPath = Path.GetFullPath(args[1]);
var output = Path.GetFullPath(args.Length > 2 ? args[2] : Path.Combine(Path.GetDirectoryName(designPath)!, "..", "artifacts"));
Directory.CreateDirectory(output);
var design = Design.Load(designPath);
var rules = new Rules(design);
var simulation = new Simulation(rules);
var checks = new Checks(rules, simulation);
checks.Run();
Console.WriteLine($"Checks: {checks.Passed} passed, {checks.Failures.Count} failed");
foreach (var failure in checks.Failures.Take(40)) Console.WriteLine("FAIL: " + failure);
WriteJson("verification.json", new
{
    design.Version,
    designSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(designPath))),
    checks.Passed, checks.Failures, realGameIntegrationTested = false,
});
if (args[0] == "verify") return checks.Failures.Count > 0 ? 1 : 0;

CoverageReport? coverage = null;
var catalogPath = Path.Combine(output, "engine-catalog.json");
if (File.Exists(catalogPath))
{
    coverage = ContentCompiler.Compile(rules, catalogPath, output,
        Path.Combine(Path.GetDirectoryName(designPath)!, "content-policies.v1.json"));
    Console.WriteLine($"Content: {coverage.MappedMonsterVariants} mapped variants; {coverage.Errors.Length} ID errors; " +
        $"{coverage.SkillsNeedingMapping} skills still require effect/master-node mapping");
}
var progression = new List<ProgressionResult>();
var combatRows = new List<object>();
var economyRows = new List<object>();
var bossRows = new List<object>();
var bossFailures = new List<string>();
foreach (var cls in design.Classes)
foreach (var build in cls.Builds)
{
    var progress = simulation.Progression(cls.Id, build.Id);
    progression.Add(progress);
    Console.WriteLine($"{cls.Id}/{build.Id}: normal={progress.NormalHours:F1}h master={progress.MasterHours:F1}h");
    if (progress.StalledNormalLevel is not null || progress.StalledMasterLevel is not null)
        bossFailures.Add($"{cls.Id}/{build.Id}: stalled normal={progress.StalledNormalLevel}, master={progress.StalledMasterLevel}");
    foreach (var level in new[] { 1, 20, 40, 80, 120, 180, 220, 300, 400 })
    foreach (var gear in design.Gear)
    {
        var p = rules.Player(cls.Id, build.Id, level, gearId: gear.Id);
        var m = rules.Monster(p.Rank);
        var single = simulation.Fight(p, m);
        var pack = simulation.Fight(p, m, level < 20 ? 1 : design.Combat.PackSize);
        var farm = simulation.Farm(p);
        combatRows.Add(new
        {
            classId = cls.Id, buildId = build.Id, level, master = 0, gear = gear.Id,
            p.Health, p.Mana, p.Power, p.Armor, p.Haste,
            hitChance = rules.HitChance(p.Accuracy, m.Evasion),
            mitigation = rules.Reduction(p.Armor, p.Rank, p.Reduction),
            singleWon = single.Won, singleTtk = single.Seconds, packWon = pack.Won, packTtk = pack.Seconds,
            noPotionSingleSurvivalSeconds = p.Health / (rules.ReceiveDamage(p, m) / m.AttackSeconds),
            farm.KillsPerHour, pack.HealthPotions, pack.ManaPotions, pack.BasicFallbackCasts,
        });
        if (gear.Id == "progression")
            economyRows.Add(new { classId = cls.Id, buildId = build.Id, level, farm });
    }
    foreach (var ml in new[] { 0, 100, 200 })
    {
        var p = rules.Player(cls.Id, build.Id, 400, ml, "excellent");
        var m = rules.Monster(p.Rank, "boss");
        var samples = Enumerable.Range(0, 64).Select(seed => simulation.Fight(p, m, seed: seed + 1700)).ToArray();
        var wins = samples.Count(s => s.Won);
        var sorted = samples.Where(s => s.Won).Select(s => s.Seconds).Order().ToArray();
        var winRate = wins / (double)samples.Length;
        var poorPlay = Enumerable.Range(0, 16).Select(seed => simulation.Fight(p, m, seed: seed + 9000,
            dodgeChance: design.Combat.BeginnerDodgeChance)).Count(s => s.Won) / 16.0;
        if (winRate < 0.80) bossFailures.Add($"{cls.Id}/{build.Id}/ML{ml}: {winRate:P0}");
        bossRows.Add(new
        {
            classId = cls.Id, buildId = build.Id, master = ml, gear = "excellent",
            samples = samples.Length, winRate, beginnerWinRate = poorPlay,
            medianWinSeconds = sorted.Length == 0 ? (double?)null : sorted[sorted.Length / 2],
            p95WinSeconds = sorted.Length == 0 ? (double?)null : sorted[(int)Math.Floor((sorted.Length - 1) * 0.95)],
            meanPotionZen = samples.Average(s => s.PotionZen),
        });
    }
}
WriteJson("progression.json", progression);
WriteJson("combat.json", combatRows);
WriteJson("economy.json", economyRows);
WriteJson("boss-simulation.json", bossRows);
WriteJson("xp-tables.json", new { normal = rules.ExperienceTable(false), master = rules.ExperienceTable(true) });
WriteJson("monster-rank-tables.json", Enumerable.Range(1, 470)
    .SelectMany(rank => design.Monsters.Ranks.Select(kind => rules.Monster(rank, kind.Id))));
WriteJson("upgrade-costs.json", design.Enhancement.Steps.Select(step => new
{
    step.Level, step.Chance, step.Pity, expectedAttempts = Rules.ExpectedAttempts(step),
    expectedJewels = Rules.ExpectedAttempts(step) * step.Jewels,
    maxJewels = step.Pity * step.Jewels,
    expectedZenAtRank400 = Rules.ExpectedAttempts(step) * rules.Upgrade(new UpgradeState(step.Level - 1), 400, 0).Zen,
}));
var budgets = new Budgets(rules);
WriteJson("equipment-budgets.json", design.Classes.SelectMany(c => new[] { 1, 80, 180, 300, 400, 470 }
    .Select(rank => new { classId = c.Id, rank, budget = budgets.Equipment(rank, c.Id, 1) })));
var arena = design.Classes.SelectMany(c => c.Builds.Select(b => budgets.Arena(c.Id, b.Id))).ToArray();
WriteJson("pvp-matrix.json", arena.SelectMany(a => arena.Select(b => budgets.Duel(a, b))));
WriteCsv("progression.csv", "class,build,normal_hours,master_hours,total_hours,max_normal_level_minutes,max_master_level_minutes",
    progression.Select(p => $"{p.ClassId},{p.BuildId},{p.NormalHours:F4},{p.MasterHours:F4},{p.NormalHours + p.MasterHours:F4},{p.MaxNormalLevelMinutes:F4},{p.MaxMasterLevelMinutes:F4}"));
var normalRange = (Min: progression.Min(p => p.NormalHours), Max: progression.Max(p => p.NormalHours));
var masterRange = (Min: progression.Min(p => p.MasterHours), Max: progression.Max(p => p.MasterHours));
var tuningFailures = new List<string>();
var bestNormal = progression.GroupBy(p => p.ClassId).Select(g => g.Min(p => p.NormalHours)).ToArray();
var bestMaster = progression.GroupBy(p => p.ClassId).Select(g => g.Min(p => p.MasterHours)).ToArray();
if (bestNormal.Max() / bestNormal.Min() > 1.25) tuningFailures.Add("Best normal build per class spread exceeds 25%.");
if (bestMaster.Max() / bestMaster.Min() > 1.25) tuningFailures.Add("Best master build per class spread exceeds 25%.");
if (normalRange.Max / normalRange.Min > 1.45 || masterRange.Max / masterRange.Min > 1.45)
    tuningFailures.Add("All-build progression spread exceeds 45%.");
var accepted = checks.Failures.Count == 0 && bossFailures.Count == 0 && tuningFailures.Count == 0 && coverage?.Errors.Length == 0;
WriteJson("acceptance.json", new
{
    acceptedAsIndependentCandidate = accepted, deployableToExistingServer = false,
    expectedBestClassNormalSpread = bestNormal.Max() / bestNormal.Min(),
    expectedBestClassMasterSpread = bestMaster.Max() / bestMaster.Min(),
    allBuildNormalSpread = normalRange.Max / normalRange.Min,
    allBuildMasterSpread = masterRange.Max / masterRange.Min,
    tuningFailures, bossFailures, coverageErrors = coverage?.Errors,
});
var report = new StringBuilder();
report.AppendLine("# 数值候选版验证报告\n");
report.AppendLine($"配置版本：{design.Version}。生成日：{DateTimeOffset.Now:yyyy-MM-dd}。\n");
report.AppendLine($"规则检查：{checks.Passed} 通过，{checks.Failures.Count} 失败。Boss 生存门槛未通过组合：{bossFailures.Count}。\n");
if (coverage is not null)
    report.AppendLine($"实际内容映射：{coverage.MappedMonsterVariants} 个地图/怪物/难度组合；{coverage.Errors.Length} 个编号错误；{coverage.SkillsNeedingMapping} 个技能仍需效果或大师节点级接入。\n");
report.AppendLine($"标准装备、标准档位下的期望模型：普通 1–400 为 {normalRange.Min:F1}–{normalRange.Max:F1} 小时；大师 0–200 为 {masterRange.Min:F1}–{masterRange.Max:F1} 小时。\n");
report.AppendLine("这不是实玩计时。模型包含命中、减伤、施法间隔、魔力、药水冷却、三目标逐只死亡、回血回蓝和走位时间；不包含真实地图寻路、网络延迟、稀有装备获得过程及玩家操作差异。\n");
report.AppendLine("| 职业 | 流派 | 普通小时 | 大师小时 | 合计小时 |\n|---|---|---:|---:|---:|");
foreach (var p in progression)
    report.AppendLine($"| {p.ClassId} | {p.BuildId} | {p.NormalHours:F1} | {p.MasterHours:F1} | {p.NormalHours + p.MasterHours:F1} |");
report.AppendLine("\n## 未通过项目\n");
if (checks.Failures.Count + bossFailures.Count == 0) report.AppendLine("本轮已执行检查没有未通过项。下面的接入限制仍然存在。\n");
foreach (var fail in checks.Failures.Concat(bossFailures)) report.AppendLine("- " + fail);
foreach (var fail in tuningFailures) report.AppendLine("- " + fail);
foreach (var fail in coverage?.Errors ?? []) report.AppendLine("- " + fail);
report.AppendLine("\n## 验证边界\n");
report.AppendLine("- 本报告来自独立的新数值内核，不是旧 OpenMU 战斗公式的复刻，也不是已接入服务端的证明。");
report.AppendLine("- engine-catalog.json 来自真实 S6 内存初始化；它验证内容编号，不验证新战斗公式已进入游戏。");
report.AppendLine("- 普通/大师全等级计时采用独立固定同阶敌人样本。地图路线映射还需验证真实刷怪密度、路径及大师实例。");
report.AppendLine("- Boss 每组 64 个固定随机种子，仅用于发现候选方案问题；胜率不等于正式服统计结论。");
report.AppendLine("- 经济的强化预算是可选消费预算，并非强制扣费；不消费时余额会继续增长。");
report.AppendLine("- 请先完成 docs/接入清单.md 中的运行时接口、客户端显示与存档迁移，再进行真实试玩。");
File.WriteAllText(Path.Combine(output, "验证报告.md"), report.ToString(), new UTF8Encoding(false));
Console.WriteLine($"Boss acceptance failures: {bossFailures.Count}");
Console.WriteLine($"Artifacts: {output}");
return accepted ? 0 : 1;

void WriteJson(string name, object value) =>
    File.WriteAllText(Path.Combine(output, name), JsonSerializer.Serialize(value, Design.Json), new UTF8Encoding(false));
void WriteCsv(string name, string header, IEnumerable<string> lines) =>
    File.WriteAllLines(Path.Combine(output, name), new[] { header }.Concat(lines), new UTF8Encoding(true));
