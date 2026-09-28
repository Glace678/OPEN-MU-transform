// <copyright file="SoloCashShopTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using System.Threading;
using System.Text.Json;
using System.IO;
using System.IO.Pipelines;
using System.Buffers.Binary;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.CashShop;
using MUnique.OpenMU.GameServer;
using MUnique.OpenMU.GameServer.MessageHandler;
using MUnique.OpenMU.GameServer.RemoteView;
using MUnique.OpenMU.Interfaces;
using MUnique.OpenMU.Network;
using MUnique.OpenMU.Network.Packets.ClientToServer;
using MUnique.OpenMU.Network.PlugIns;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.Persistence.InMemory;
using MUnique.OpenMU.PlugIns;
using Nito.AsyncEx;
using Basic = MUnique.OpenMU.Persistence.BasicModel;

/// <summary>
/// Concentrated coverage for the local shop: validation, account ledger and atomic delivery.
/// </summary>
[TestFixture]
public class SoloCashShopTests
{
    private readonly SoloCashShopService _shop = new();
    private Player _player = null!;
    private SoloCashShopCatalog.Offer _offer = null!;
    private FaultContext _context = null!;

    [SetUp]
    public async Task SetUpAsync()
    {
        var template = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        var configuration = template.GameContext.Configuration;
        configuration.GlobalBaseAttributeValues.Add(new ConstValueAttribute(
            1, new AttributeDefinition(SoloBalance.ProfileAttributeId, "Solo", string.Empty)));
        var definition = new Basic.ItemDefinition { Group = 14, Number = 13, Width = 1, Height = 1, Durability = 1, Name = "Bless", MaximumItemLevel = 15 };
        configuration.Items.Add(definition);
        var store = new Basic.ItemStorage();
        store.Items.Add(new Basic.Item { Definition = definition, Durability = 1, HasSkill = true });
        Mock.Get(configuration).Setup(c => c.Monsters).Returns([new Basic.MonsterDefinition { MerchantStore = store }]);
        var provider = new FaultProvider();
        var initializer = new MapInitializer(configuration, new NullLogger<MapInitializer>(), NullDropGenerator.Instance, null);
        var game = new GameServerContext(
            new Basic.GameServerDefinition { GameConfiguration = configuration, ServerConfiguration = new Basic.GameServerConfiguration() },
            Mock.Of<IGuildServer>(), Mock.Of<IEventPublisher>(), Mock.Of<ILoginServer>(), Mock.Of<IFriendServer>(),
            provider, initializer, NullLoggerFactory.Instance,
            new PlugInManager(null, NullLoggerFactory.Instance, null, null), NullDropGenerator.Instance, new ConfigurationChangeMediator());
        initializer.PlugInManager = game.PlugInManager;
        initializer.PathFinderPool = game.PathFinderPool;
        this._player = await PlayerTestHelper.CreatePlayerAsync(game).ConfigureAwait(false);
        this._context = (FaultContext)this._player.PersistenceContext;
        this._player.IsAlive = true;
        await this._player.PlayerState.TryAdvanceToAsync(PlayerState.EnteredWorld).ConfigureAwait(false);
        this._player.CurrentMap!.Terrain.SafezoneMap[this._player.Position.X, this._player.Position.Y] = true;
        this._player.SelectedCharacter!.Name = "SoloHero";
        Mock.Get(this._player.Account!).Setup(a => a.Characters).Returns(
            [this._player.SelectedCharacter, new Basic.Character { Name = "SoloAlt" }]);
        this._offer = SoloCashShopCatalog.GetOffers(configuration).Single();
    }

    [Test]
    public async Task WelcomeCreditIsGrantedOnceAsync()
    {
        Assert.That(await this._shop.InitializeAsync(this._player).ConfigureAwait(false), Is.Zero);
        Assert.That(await this.BuyAsync().ConfigureAwait(false), Is.Zero);
        var saved = this._player.Account!.SoloCashShopData;
        Assert.That(await this._shop.InitializeAsync(this._player).ConfigureAwait(false), Is.Zero);
        Assert.That(this._player.Account.SoloCashShopData, Is.EqualTo(saved));
        Assert.That(this.State.Credit, Is.EqualTo(SoloCashShopState.WelcomeCredit - this._offer.Price));
        Assert.That(this.State.Entries.Single().ItemCode, Is.EqualTo(this._offer.ItemCode));
    }

    [TestCase("offer")]
    [TestCase("category")]
    [TestCase("price")]
    [TestCase("item")]
    [TestCase("coin")]
    [TestCase("mileage")]
    public async Task ForgedPurchaseIsRejectedWithoutChargeAsync(string field)
    {
        var result = await this._shop.BuyAsync(this._player,
            this._offer.Id + (field == "offer" ? 1u : 0),
            (uint)(this._offer.Category + (field == "category" ? 1 : 0)),
            this._offer.Id + (field == "price" ? 1u : 0),
            (ushort)(this._offer.ItemCode + (field == "item" ? 1 : 0)),
            field == "coin" ? 1u : SoloCashShopCatalog.CoinIndex,
            field == "mileage" ? (byte)1 : (byte)0).ConfigureAwait(false);
        Assert.That(result, Is.Not.Zero);
        Assert.That(this._player.Account!.SoloCashShopData, Is.Empty);
    }

    [Test]
    public async Task InsufficientCreditAndFullStorageAreRejectedAsync()
    {
        this._player.Account!.SoloCashShopData = new SoloCashShopState { Credit = 0 }.Serialize();
        Assert.That(await this.BuyAsync().ConfigureAwait(false), Is.EqualTo(1));
        var full = new SoloCashShopState();
        for (var i = 0; i < SoloCashShopState.StorageCapacity; i++)
        {
            full.Entries.Add(new SoloCashShopState.Entry(full.NextId++, this._offer.Id, this._offer.ItemCode, 0, 1, this._offer.Price));
        }
        this._player.Account.SoloCashShopData = full.Serialize();
        Assert.That(await this.BuyAsync().ConfigureAwait(false), Is.EqualTo(2));
        Assert.That(this.State.Credit, Is.EqualTo(SoloCashShopState.WelcomeCredit));
    }

    [Test]
    public async Task ClaimSurvivesLedgerReloadAndRejectsReplayAsync()
    {
        Assert.That(await this.BuyAsync().ConfigureAwait(false), Is.Zero);
        this._player.Account!.SoloCashShopData = this.State.Serialize();
        var entry = this.State.Entries.Single();
        Assert.That(await this._shop.ClaimAsync(this._player, entry.Id, entry.Id, (ushort)(entry.ItemCode + 1), (byte)'P').ConfigureAwait(false), Is.EqualTo(1));
        Assert.That(await this.ClaimAsync(entry).ConfigureAwait(false), Is.Zero);
        Assert.That(this._player.Inventory!.Items.Single().Definition, Is.SameAs(this._offer.Definition));
        Assert.That(this._player.Inventory.Items.Single().HasSkill, Is.True);
        Assert.That(this.State.Entries, Is.Empty);
        Assert.That(await this.ClaimAsync(entry).ConfigureAwait(false), Is.EqualTo(1));
        Assert.That(this._player.Inventory.Items.Count(), Is.EqualTo(1));
    }

    [Test]
    public async Task FullBagKeepsPurchaseUntilSpaceIsAvailableAsync()
    {
        await this.BuyAsync().ConfigureAwait(false);
        while (await this._player.Inventory!.AddItemAsync(new Basic.Item { Definition = this._offer.Definition, Durability = 1 }).ConfigureAwait(false))
        {
        }
        var entry = this.State.Entries.Single();
        Assert.That(await this.ClaimAsync(entry).ConfigureAwait(false), Is.EqualTo(21));
        Assert.That(this.State.Entries.Count, Is.EqualTo(1));
        await this._player.Inventory.RemoveItemAsync(this._player.Inventory.Items.First()).ConfigureAwait(false);
        Assert.That(await this.ClaimAsync(entry).ConfigureAwait(false), Is.Zero);
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task FailedSaveRollsBackPurchaseClaimAndExchangeAsync(bool throws)
    {
        await this._shop.InitializeAsync(this._player).ConfigureAwait(false);
        var before = this._player.Account!.SoloCashShopData;
        this._context.Fail = true;
        this._context.Throws = throws;
        Assert.That(await this.BuyAsync().ConfigureAwait(false), Is.EqualTo(255));
        Assert.That(this._player.Account.SoloCashShopData, Is.EqualTo(before));
        this._player.Money = 2000;
        Assert.That(await this._shop.ExchangeAsync(this._player).ConfigureAwait(false), Is.EqualTo(255));
        Assert.That(this._player.Money, Is.EqualTo(2000));
        this._context.Fail = false;
        await this.BuyAsync().ConfigureAwait(false);
        before = this._player.Account.SoloCashShopData;
        this._context.Fail = true;
        var entry = this.State.Entries.Single();
        Assert.That(await this.ClaimAsync(entry).ConfigureAwait(false), Is.EqualTo(255));
        Assert.That(this._player.Inventory!.Items, Is.Empty);
        Assert.That(this._player.Account.SoloCashShopData, Is.EqualTo(before));
        this._context.Fail = false;
        Assert.That(await this.ClaimAsync(entry).ConfigureAwait(false), Is.Zero);
    }

    [Test]
    public async Task GoldExchangeHasBoundsAndPreservesOldGoldAsync()
    {
        this._player.Money = SoloCashShopService.ExchangeGold - 1;
        Assert.That(await this._shop.ExchangeAsync(this._player).ConfigureAwait(false), Is.EqualTo(1));
        Assert.That(this._player.Money, Is.EqualTo(SoloCashShopService.ExchangeGold - 1));
        this._player.Money = SoloCashShopService.ExchangeGold;
        Assert.That(await this._shop.ExchangeAsync(this._player).ConfigureAwait(false), Is.Zero);
        Assert.That(this._player.Money, Is.Zero);
        Assert.That(this.State.Credit, Is.EqualTo(SoloCashShopState.WelcomeCredit + SoloCashShopService.ExchangeCredit));
        this._player.Account!.SoloCashShopData = new SoloCashShopState { Credit = SoloCashShopState.MaximumCredit }.Serialize();
        this._player.Money = SoloCashShopService.ExchangeGold;
        Assert.That(await this._shop.ExchangeAsync(this._player).ConfigureAwait(false), Is.EqualTo(1));
        Assert.That(this._player.Money, Is.EqualTo(SoloCashShopService.ExchangeGold));
    }

    [Test]
    public async Task GiftOnlyBelongsToNamedCharacterOfSameAccountAsync()
    {
        Assert.That(await this.BuyAsync("OtherUser").ConfigureAwait(false), Is.EqualTo(10));
        Assert.That(await this.BuyAsync("SoloAlt").ConfigureAwait(false), Is.Zero);
        var entry = this.State.Entries.Single();
        Assert.That(await this.ClaimAsync(entry).ConfigureAwait(false), Is.EqualTo(1));
        this._player.SelectedCharacter!.Name = "SoloAlt";
        Assert.That(await this.ClaimAsync(entry).ConfigureAwait(false), Is.Zero);
    }

    [Test]
    public async Task DeletionCannotRefundOrDeleteTwiceAsync()
    {
        await this.BuyAsync().ConfigureAwait(false);
        var entry = this.State.Entries.Single();
        var credit = this.State.Credit;
        Assert.That(await this._shop.DeleteAsync(this._player, entry.Id, entry.Id + 1, (byte)'P').ConfigureAwait(false), Is.EqualTo(1));
        Assert.That(await this._shop.DeleteAsync(this._player, entry.Id, entry.Id, (byte)'P').ConfigureAwait(false), Is.Zero);
        Assert.That(await this._shop.DeleteAsync(this._player, entry.Id, entry.Id, (byte)'P').ConfigureAwait(false), Is.EqualTo(1));
        Assert.That(this.State.Credit, Is.EqualTo(credit));
    }

    [TestCase("non-solo")]
    [TestCase("dead")]
    [TestCase("trade")]
    [TestCase("outside-town")]
    [TestCase("template")]
    public async Task InvalidPlayerStatesCannotTradeAsync(string state)
    {
        switch (state)
        {
            case "non-solo": this._player.GameContext.Configuration.GlobalBaseAttributeValues.Clear(); break;
            case "dead": this._player.IsAlive = false; break;
            case "trade": await this._player.PlayerState.TryAdvanceToAsync(PlayerState.TradeRequested).ConfigureAwait(false); break;
            case "outside-town": this._player.CurrentMap!.Terrain.SafezoneMap[this._player.Position.X, this._player.Position.Y] = false; break;
            case "template": this._player.Account!.IsTemplate = true; break;
        }
        Assert.That(await this.BuyAsync().ConfigureAwait(false), Is.Not.Zero);
        Assert.That(await this._shop.ExchangeAsync(this._player).ConfigureAwait(false), Is.Not.Zero);
        Assert.That(this._player.Account!.SoloCashShopData, Is.Empty);
    }

    [TestCase("{\"Version\":2}")]
    [TestCase("{\"Credit\":-1}")]
    [TestCase("{\"NextId\":0}")]
    [TestCase("{\"Entries\":null}")]
    public void CorruptOrNewerLedgerNeverResetsCredit(string json) =>
        Assert.Throws<InvalidOperationException>(() => SoloCashShopState.Read(json));

    [Test]
    public void MalformedJsonNeverResetsCredit() =>
        Assert.Throws<JsonException>(() => SoloCashShopState.Read("{broken"));

    [Test]
    public async Task ConcurrentClaimsDeliverOnlyOnceAsync()
    {
        await this.BuyAsync().ConfigureAwait(false);
        var entry = this.State.Entries.Single();
        var results = await Task.WhenAll(Enumerable.Range(0, 16).Select(_ => this.ClaimAsync(entry).AsTask())).ConfigureAwait(false);
        Assert.That(results.Count(r => r == 0), Is.EqualTo(1));
        Assert.That(this._player.Inventory!.Items.Count(), Is.EqualTo(1));
    }

    [Test]
    public async Task LegacyWirePurchaseStorageAndClaimRoundTripAsync()
    {
        var (player, stream) = await this.CreateRemoteAsync().ConfigureAwait(false);
        using var output = stream;
        var handler = new CashShopHandlerPlugIn();
        await handler.HandlePacketAsync(player, new byte[] { 0xC1, 5, 0xD2, 2, 0 }).ConfigureAwait(false);
        var catalog = ReadPackets(output);
        Assert.That(catalog.Select(p => p[3]), Is.EqualTo(new byte[] { 0xF0, 0xF1, 0xF2, 2 }));
        Assert.That(catalog.Select(p => p.Length), Is.EqualTo(new[] { 8, 114, 4, 5 }));
        Assert.That(BinaryPrimitives.ReadUInt32LittleEndian(catalog[1].AsSpan(4)), Is.EqualTo(this._offer.Id));
        Assert.That(BinaryPrimitives.ReadInt32LittleEndian(catalog[1].AsSpan(12)), Is.EqualTo(this._offer.Price));
        Assert.That(catalog[3][4], Is.EqualTo(1));

        await handler.HandlePacketAsync(player, new byte[] { 0xC1, 4, 0xD2, 1 }).ConfigureAwait(false);
        var points = ReadPackets(output).Single();
        Assert.That(points.Length, Is.EqualTo(45));
        Assert.That(BinaryPrimitives.ReadDoubleLittleEndian(points.AsSpan(13)), Is.EqualTo(SoloCashShopState.WelcomeCredit));

        var request = new byte[CashShopItemBuyRequest.Length];
        _ = new CashShopItemBuyRequest(request)
        {
            PackageMainIndex = this._offer.Id, ProductMainIndex = this._offer.Id,
            Category = this._offer.Category, ItemIndex = this._offer.ItemCode,
            CoinIndex = SoloCashShopCatalog.CoinIndex,
        };
        await handler.HandlePacketAsync(player, request).ConfigureAwait(false);
        Assert.That(ReadPackets(output).Single(), Is.EqualTo(new byte[] { 0xC1, 9, 0xD2, 3, 0, 255, 255, 255, 255 }));

        request = new byte[CashShopStorageListRequest.Length];
        _ = new CashShopStorageListRequest(request) { PageIndex = uint.MaxValue, InventoryType = (byte)'S' };
        await handler.HandlePacketAsync(player, request).ConfigureAwait(false);
        var storage = ReadPackets(output);
        Assert.That(storage.Select(p => p.Length), Is.EqualTo(new[] { 12, 33 }));
        Assert.That(BinaryPrimitives.ReadUInt16LittleEndian(storage[0].AsSpan(8)), Is.EqualTo(1));
        var id = BinaryPrimitives.ReadUInt32LittleEndian(storage[1].AsSpan(4));
        request = new byte[CashShopStorageItemConsumeRequest.Length];
        _ = new CashShopStorageItemConsumeRequest(request) { BaseItemCode = id, MainItemCode = id, ItemIndex = this._offer.ItemCode, ProductType = (byte)'P' };
        await handler.HandlePacketAsync(player, request).ConfigureAwait(false);
        Assert.That(ReadPackets(output).Single()[4], Is.Zero);
        await handler.HandlePacketAsync(player, request).ConfigureAwait(false);
        Assert.That(ReadPackets(output).Single()[4], Is.EqualTo(1));
        Assert.That(player.Inventory!.Items.Count(), Is.EqualTo(1));
    }

    [Test]
    public async Task HandlerRejectsTruncationAndInactivePointRequestsAsync()
    {
        var (player, stream) = await this.CreateRemoteAsync().ConfigureAwait(false);
        using var output = stream;
        var handler = new CashShopHandlerPlugIn();
        for (var length = 4; length < CashShopItemBuyRequest.Length; length++)
        {
            var packet = new byte[length];
            packet[0] = 0xC1;
            packet[1] = (byte)length;
            packet[2] = 0xD2;
            packet[3] = 3;
            await handler.HandlePacketAsync(player, packet).ConfigureAwait(false);
        }
        player.IsAlive = false;
        await handler.HandlePacketAsync(player, new byte[] { 0xC1, 4, 0xD2, 1 }).ConfigureAwait(false);
        Assert.That(output.Length, Is.Zero, "Never send a short result packet where the client expects five doubles.");
    }

    private async Task<(RemotePlayer Player, MemoryStream Output)> CreateRemoteAsync()
    {
        var output = new MemoryStream();
        var writer = PipeWriter.Create(output, new StreamPipeWriterOptions(leaveOpen: true));
        var connection = new Mock<IConnection>();
        connection.SetupGet(c => c.Connected).Returns(true);
        connection.SetupGet(c => c.Output).Returns(writer);
        connection.SetupGet(c => c.OutputLock).Returns(new AsyncLock());
        var player = new RemotePlayer((IGameServerContext)this._player.GameContext, connection.Object, new ClientVersion(6, 3, ClientLanguage.English))
        {
            Account = this._player.Account,
        };
        await player.PlayerState.TryAdvanceToAsync(PlayerState.LoginScreen).ConfigureAwait(false);
        await player.PlayerState.TryAdvanceToAsync(PlayerState.Authenticated).ConfigureAwait(false);
        await player.PlayerState.TryAdvanceToAsync(PlayerState.CharacterSelection).ConfigureAwait(false);
        await player.SetSelectedCharacterAsync(this._player.SelectedCharacter!).ConfigureAwait(false);
        player.IsAlive = true;
        player.CurrentMap!.Terrain.SafezoneMap[player.Position.X, player.Position.Y] = true;
        return (player, output);
    }

    private static List<byte[]> ReadPackets(MemoryStream output)
    {
        var bytes = output.ToArray();
        output.SetLength(0);
        output.Position = 0;
        var packets = new List<byte[]>();
        for (var index = 0; index < bytes.Length;)
        {
            Assert.That(bytes[index], Is.EqualTo(0xC1));
            var length = bytes[index + 1];
            Assert.That(length, Is.GreaterThanOrEqualTo(4));
            Assert.That(index + length, Is.LessThanOrEqualTo(bytes.Length));
            packets.Add(bytes.AsSpan(index, length).ToArray());
            index += length;
        }
        return packets;
    }

    private SoloCashShopState State => SoloCashShopState.Read(this._player.Account!.SoloCashShopData);

    private ValueTask<byte> BuyAsync(string recipient = "") =>
        this._shop.BuyAsync(this._player, this._offer.Id, this._offer.Category, this._offer.Id,
            this._offer.ItemCode, SoloCashShopCatalog.CoinIndex, 0, recipient);

    private ValueTask<byte> ClaimAsync(SoloCashShopState.Entry entry) =>
        this._shop.ClaimAsync(this._player, entry.Id, entry.Id, entry.ItemCode, (byte)'P');

    private sealed class FaultProvider : InMemoryPersistenceContextProvider, IPersistenceContextProvider
    {
        IPlayerContext IPersistenceContextProvider.CreateNewPlayerContext(GameConfiguration configuration) =>
            new FaultContext((InMemoryRepositoryProvider)this.RepositoryProvider);
    }

    private sealed class FaultContext(InMemoryRepositoryProvider provider) : PlayerInMemoryContext(provider), IPlayerContext
    {
        public bool Fail { get; set; }
        public bool Throws { get; set; }

        ValueTask<bool> IContext.SaveChangesAsync(CancellationToken cancellationToken) =>
            this.Fail ? this.Throws ? throw new InvalidOperationException("Injected save failure.") : ValueTask.FromResult(false)
                : this.SaveChangesAsync(cancellationToken);
    }
}
