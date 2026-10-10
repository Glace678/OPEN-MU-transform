// <copyright file="SoloCombatBalancePgUpgradeTests.cs" company="MUnique OpenMU">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Tests;

using System.Text.Json;
using System.Threading;
using Microsoft.Extensions.Logging.Abstractions;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.Interfaces;
using MUnique.OpenMU.Persistence.EntityFramework;
using MUnique.OpenMU.Persistence.EntityFramework.Json;
using MUnique.OpenMU.Persistence.Initialization.Updates;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Upgrades a disposable PostgreSQL copy (openmu_r4upgrade) of the pre-R4 production dump through the
/// real EF/Npgsql persistence: solo conversion (if not already installed) then v110 mandatory update.
/// This is [Explicit] and only runs when OPENMU_R4_UPGRADE_CS is set; it mutates the throwaway copy and
/// never touches the live openmu database.
/// </summary>
[TestFixture, Explicit("requires the disposable openmu_r4upgrade PostgreSQL copy")]
public class SoloCombatBalancePgUpgradeTests
{
    private static readonly Guid MaximumHealthId = new("A6C39A5C-295F-415E-A314-5E9F9A748D27");
    private static readonly Guid DefenseBaseId = new("EB098C46-60D4-4CA6-BBD4-5B6270A1407B");
    private static readonly Guid MinAtkId = new("3E8D6A02-E973-4AE4-9DF3-CDDC3D3183B3");
    private static readonly Guid MaxAtkId = new("8A918EA2-893A-48B2-A684-3E71526CA71F");
    private static readonly Guid AtkRatePvmId = new("1129442A-E1C7-4240-8866-B781C2838C25");
    private static readonly Guid DefRatePvmId = new("C520DD2D-1B06-4392-95EE-3C41F33E68DA");

    private static bool _convertersRegistered;
    private static void EnsureConvertersRegistered()
    {
        if (_convertersRegistered) { return; }
        JsonConverterRegistry.RegisterConverter(new LocalizedStringJsonConverter());
        JsonConverterRegistry.RegisterConverter(new BinaryAsHexJsonConverter());
        _convertersRegistered = true;
    }

    [Test, Explicit("read-only bisection: loads config from OPENMU_R4_UPGRADE_CS without mutating")]
    public async Task ReadOnlyLoadConfigurationBisectionAsync()
    {
        EnsureConvertersRegistered();
        var cs = Environment.GetEnvironmentVariable("OPENMU_R4_UPGRADE_CS");
        if (string.IsNullOrEmpty(cs)) { Assert.Ignore(); return; }
        if (!ConnectionConfigurator.IsInitialized) { ConnectionConfigurator.Initialize(new InlineConnectionProvider(cs)); }
        var provider = new PersistenceContextProvider(NullLoggerFactory.Instance, null);
        using var read = provider.CreateNewConfigurationContext();
        var configs = await read.GetAsync<GameConfiguration>().ConfigureAwait(false);
        Assert.That(configs.Count(), Is.GreaterThan(0));
        TestContext.WriteLine($"loaded {configs.Count()} configuration(s) read-only OK");
    }

    [Test]    public async Task UpgradePgCopyThroughSoloThenV110Async()
    {
        var cs = Environment.GetEnvironmentVariable("OPENMU_R4_UPGRADE_CS");
        if (string.IsNullOrEmpty(cs))
        {
            Assert.Ignore("Set OPENMU_R4_UPGRADE_CS to run the real PostgreSQL copy upgrade.");
            return;
        }

        EnsureConvertersRegistered();
        if (!ConnectionConfigurator.IsInitialized)
        {
            ConnectionConfigurator.Initialize(new InlineConnectionProvider(cs));
        }

        var provider = new PersistenceContextProvider(NullLoggerFactory.Instance, null);
        // Equivalent startup: bring the restored schema up to the current model before loading config.
        await provider.ApplyAllPendingUpdatesAsync().ConfigureAwait(false);
        TestContext.WriteLine("pending migrations applied.");

        GameConfiguration configuration;
        using (var read = provider.CreateNewConfigurationContext())
        {
            configuration = (await read.GetAsync<GameConfiguration>().ConfigureAwait(false)).Single();
        }

        bool soloWasEnabled = SoloBalance.IsEnabled(configuration);
        TestContext.WriteLine($"soloWasEnabled={soloWasEnabled}");

        float Hp(short num) => MonsterAttr(configuration, num, MaximumHealthId);
        float Def(short num) => MonsterAttr(configuration, num, DefenseBaseId);
        TestContext.WriteLine($"BEFORE  spider#3 hp={Hp(3)} def={Def(3)} | larva#12 hp={Hp(12)} def={Def(12)} | dk#10 hp={Hp(10)} def={Def(10)}");

        // Equivalent startup sequence: solo conversion (first run only), then v110.
        if (!soloWasEnabled)
        {
            using var soloContext = provider.CreateNewContext(configuration);
            soloContext.Attach(configuration);
            new SoloBalanceInitializer(soloContext, configuration).Initialize();
            await soloContext.SaveChangesAsync().ConfigureAwait(false);
            TestContext.WriteLine("solo conversion applied.");
        }

        using var context = provider.CreateNewContext(configuration);
        context.Attach(configuration);
        await new SoloCombatBalanceSeason6UpdatePlugIn().ApplyUpdateAsync(context, configuration).ConfigureAwait(false);
        await context.SaveChangesAsync().ConfigureAwait(false);
        TestContext.WriteLine("v110 applied and saved.");

        // Read back fresh from the DB (new context) to prove persistence, not in-memory caching.
        using var verifyRead = provider.CreateNewConfigurationContext();
        var verify = (await verifyRead.GetAsync<GameConfiguration>().ConfigureAwait(false)).Single();
        float VHp(short num) => MonsterAttr(verify, num, MaximumHealthId);
        float VDef(short num) => MonsterAttr(verify, num, DefenseBaseId);
        TestContext.WriteLine($"AFTER   spider#3 hp={VHp(3)} def={VDef(3)} | larva#12 hp={VHp(12)} def={VDef(12)} | dk#10 hp={VHp(10)} def={VDef(10)}");

        // Written values must equal the embedded B1v2 post-solo targets exactly (6 attrs); the table's
        // authoritative TtkDKs is B1's own model output and must land in the newbie 4-8s band.
        using var table = LoadMonsterTable();
        foreach (var num in new short[] { 3, 26, 28 })
        {
            var row = table.RootElement.EnumerateArray().First(r => r.GetProperty("Num").GetInt32() == num);
            Assert.That(VHp(num), Is.EqualTo(row.GetProperty("PostHp").GetDouble()).Within(1), $"PG #{num} HP == PostHp");
            Assert.That(VDef(num), Is.EqualTo(row.GetProperty("PostDefBase").GetDouble()).Within(1), $"PG #{num} DefBase == PostDefBase");
            Assert.That(MonsterAttr(verify, num, MinAtkId), Is.EqualTo(row.GetProperty("PostMinAtk").GetDouble()).Within(1), $"PG #{num} MinAtk == PostMinAtk");
            Assert.That(MonsterAttr(verify, num, MaxAtkId), Is.EqualTo(row.GetProperty("PostMaxAtk").GetDouble()).Within(1), $"PG #{num} MaxAtk == PostMaxAtk");
            Assert.That(MonsterAttr(verify, num, AtkRatePvmId), Is.EqualTo(row.GetProperty("PostAtkRatePvm").GetDouble()).Within(1), $"PG #{num} AtkRate == PostAtkRatePvm");
            Assert.That(MonsterAttr(verify, num, DefRatePvmId), Is.EqualTo(row.GetProperty("PostDefRatePvm").GetDouble()).Within(1), $"PG #{num} DefRate == PostDefRatePvm");
            double level = row.GetProperty("Level").GetDouble();
            double ttk = row.GetProperty("TtkDKs").GetDouble();
            string mobName = row.GetProperty("Name").GetString() ?? "?";
            double postHp = row.GetProperty("PostHp").GetDouble();
            TestContext.WriteLine($"#{num} {mobName} lv{level} postHp={postHp} ttkDK={ttk}s");
            Assert.That(ttk, Is.InRange(4.0, 8.0), $"newbie #{num} DK TTK in 4-8s");
        }

        // Idempotent: run v110 a second time, values must not change.
        using var again = provider.CreateNewContext(configuration);
        again.Attach(configuration);
        await new SoloCombatBalanceSeason6UpdatePlugIn().ApplyUpdateAsync(again, configuration).ConfigureAwait(false);
        await again.SaveChangesAsync().ConfigureAwait(false);
        using var verify2 = provider.CreateNewConfigurationContext();
        var verify2Cfg = (await verify2.GetAsync<GameConfiguration>().ConfigureAwait(false)).Single();
        Assert.That(MonsterAttr(verify2Cfg, 3, MaximumHealthId), Is.EqualTo(VHp(3)).Within(1), "second v110 run must be idempotent");
    }

    private static JsonDocument LoadMonsterTable()
    {
        var asm = typeof(SoloCombatBalanceSeason6UpdatePlugIn).Assembly;
        var name = asm.GetManifestResourceNames().First(n => n.EndsWith("MonsterTargets.json", StringComparison.Ordinal));
        return JsonDocument.Parse(asm.GetManifestResourceStream(name)!);
    }

    private static float MonsterAttr(GameConfiguration cfg, short number, Guid attributeId) =>
        cfg.Monsters.First(m => m.Number == number).Attributes.First(a => a.AttributeDefinition!.Id == attributeId).Value;

    private static double ComputeTtkDk(double level, double postHp, double postDef)
    {
        double bm = Interp(
            new double[] { 1, 5, 10, 15, 20, 30, 40, 50, 60, 80, 100, 120, 140, 150 },
            new double[] { 39, 65, 100, 145, 200, 320, 470, 650, 860, 1350, 1950, 2700, 3600, 4200 },
            level);
        const double kDk = 37.0 / 38.4;
        double clsBase = bm * kDk;
        double hitChance = level <= 20 ? 0.80 : 0.90;
        double effective = Math.Max(clsBase - postDef, Math.Max(4.0, level / 10.0));
        return postHp / (effective * hitChance * 1.0);
    }

    private static double Interp(double[] xs, double[] ys, double x)
    {
        if (x <= xs[0]) return ys[0];
        if (x >= xs[^1]) return ys[^1];
        for (int i = 0; i < xs.Length - 1; i++)
        {
            if (x <= xs[i + 1])
            {
                double t = (x - xs[i]) / (xs[i + 1] - xs[i]);
                return ys[i] + t * (ys[i + 1] - ys[i]);
            }
        }

        return ys[^1];
    }

    private sealed class InlineConnectionProvider : IDatabaseConnectionSettingProvider
    {
        private readonly string _cs;

        public InlineConnectionProvider(string cs) => this._cs = cs;

        public Task? Initialization => null;

        public Task InitializeAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public ConnectionSetting GetConnectionSetting<TContextType>()
            where TContextType : Microsoft.EntityFrameworkCore.DbContext => this.GetConnectionSetting(typeof(TContextType));

        public ConnectionSetting GetConnectionSetting(Type contextType) => new()
        {
            ContextTypeName = contextType.Name,
            ConnectionString = this._cs,
            DatabaseEngine = DatabaseEngine.Npgsql,
        };
    }
}
