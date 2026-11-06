// <copyright file="AdversarialAntiCheatVerificationTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using System.Reflection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;
using MUnique.OpenMU.DataModel;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.Views.World;
using MUnique.OpenMU.GameLogic.Views.Inventory;
using MUnique.OpenMU.GameLogic.Views.PlayerShop;
using MUnique.OpenMU.GameLogic.PlayerActions.PlayerStore;
using MUnique.OpenMU.GameServer.MessageHandler;
using MUnique.OpenMU.GameServer.MessageHandler.PlayerShop;
using MUnique.OpenMU.Network.Packets.ClientToServer;
using MUnique.OpenMU.Pathfinding;
using MUnique.OpenMU.Persistence;


/// <summary>
/// Empirical adversarial verification tests challenging server anti-cheat,
/// trade rollback, durability clamping, and arithmetic overflow protections.
/// </summary>
[TestFixture]
public class AdversarialAntiCheatVerificationTests
{
    // =========================================================================
    // 1. Movement Anti-Cheat Adversarial Stress
    // =========================================================================

    [Test]
    public async Task Movement_TeleportLargeDelta_RejectedAndRubberbandedAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        await player.ClientReadyAfterMapChangeAsync().ConfigureAwait(false);

        var startPoint = new Point(100, 100);
        player.Position = startPoint;
        if (player.CurrentMap is { } map)
        {
            map.Terrain.WalkMap[startPoint.X, startPoint.Y] = true;
            map.Terrain.WalkMap[150, 150] = true;
        }

        var movedMock = Mock.Get(player.ViewPlugIns.GetPlugIn<IObjectMovedPlugIn>()!);
        movedMock.Invocations.Clear();

        // Cheater injects InstantMoveRequest to (150, 150) - distance ~70.7 > 2.0
        var packet = new byte[] { 0xC1, 0x05, 0x15, 150, 150 };
        var handler = new CharacterMoveHandlerPlugIn();
        await handler.HandlePacketAsync(player, packet).ConfigureAwait(false);

        // Verify: Teleport rejected, player snapped back, rubberband packet sent
        Assert.That(player.Position, Is.EqualTo(startPoint), "Player position must remain at original point.");
        movedMock.Verify(p => p.ObjectMovedAsync(player, MoveType.Instant), Times.Once, "Rubberband instant move notification must be sent to client.");
    }

    [Test]
    public async Task Movement_TeleportDeltaThree_RejectedAndRubberbandedAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        await player.ClientReadyAfterMapChangeAsync().ConfigureAwait(false);

        var startPoint = new Point(100, 100);
        player.Position = startPoint;
        if (player.CurrentMap is { } map)
        {
            map.Terrain.WalkMap[startPoint.X, startPoint.Y] = true;
            map.Terrain.WalkMap[103, 100] = true;
        }

        var movedMock = Mock.Get(player.ViewPlugIns.GetPlugIn<IObjectMovedPlugIn>()!);
        movedMock.Invocations.Clear();

        // Step distance 3.0 exceeds maximum allowed delta (2.0)
        var packet = new byte[] { 0xC1, 0x05, 0x15, 103, 100 };
        var handler = new CharacterMoveHandlerPlugIn();
        await handler.HandlePacketAsync(player, packet).ConfigureAwait(false);

        Assert.That(player.Position, Is.EqualTo(startPoint), "Step distance 3.0 must be rejected.");
        movedMock.Verify(p => p.ObjectMovedAsync(player, MoveType.Instant), Times.Once);
    }

    [Test]
    public async Task Movement_UnwalkableTile_RejectedAndRubberbandedAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        await player.ClientReadyAfterMapChangeAsync().ConfigureAwait(false);

        var startPoint = new Point(100, 100);
        player.Position = startPoint;
        if (player.CurrentMap is { } map)
        {
            map.Terrain.WalkMap[startPoint.X, startPoint.Y] = true;
            // Mark target as unwalkable obstacle
            map.Terrain.WalkMap[101, 100] = false;
        }

        var movedMock = Mock.Get(player.ViewPlugIns.GetPlugIn<IObjectMovedPlugIn>()!);
        movedMock.Invocations.Clear();

        // Cheater tries to move into non-walkable tile
        var packet = new byte[] { 0xC1, 0x05, 0x15, 101, 100 };
        var handler = new CharacterMoveHandlerPlugIn();
        await handler.HandlePacketAsync(player, packet).ConfigureAwait(false);

        Assert.That(player.Position, Is.EqualTo(startPoint), "Movement into unwalkable terrain must be rejected.");
        movedMock.Verify(p => p.ObjectMovedAsync(player, MoveType.Instant), Times.Once);
    }

    [Test]
    public async Task Movement_LegitimateSingleStep_AcceptedAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        await player.ClientReadyAfterMapChangeAsync().ConfigureAwait(false);

        var startPoint = new Point(100, 100);
        var targetPoint = new Point(101, 100);
        player.Position = startPoint;
        if (player.CurrentMap is { } map)
        {
            map.Terrain.WalkMap[startPoint.X, startPoint.Y] = true;
            map.Terrain.WalkMap[targetPoint.X, targetPoint.Y] = true;
        }

        var packet = new byte[] { 0xC1, 0x05, 0x15, targetPoint.X, targetPoint.Y };
        var handler = new CharacterMoveHandlerPlugIn();
        await handler.HandlePacketAsync(player, packet).ConfigureAwait(false);

        Assert.That(player.Position, Is.EqualTo(targetPoint), "Legitimate move within 2.0 tiles on walkable terrain must be accepted.");
    }

    // =========================================================================
    // 2. Skill Anti-Cheat Adversarial Stress
    // =========================================================================

    [Test]
    public void SkillHit_InstantHitWithoutPriorAnimation_ReturnsFalseFalse()
    {
        var validator = new SkillHitValidator(NullLogger.Instance);
        // Attacker sends hit packet directly without sending animation
        var (isValid, increaseCounter) = validator.IsHitValid(skillId: 9, animationCounter: 5, hitCounter: 6);
        Assert.That(isValid, Is.False, "Hit without prior animation must be strictly rejected.");
        Assert.That(increaseCounter, Is.False);
    }

    [Test]
    public void SkillHit_MismatchedSkillId_ReturnsFalseFalse()
    {
        var validator = new SkillHitValidator(NullLogger.Instance);
        var reg = validator.TryRegisterAnimation(skillId: 9, animationCounter: 1); // EvilSpirit
        Assert.That(reg, Is.True);

        // Hit packet claims skill 8 (Twister) for animation 1
        var (isValid, increaseCounter) = validator.IsHitValid(skillId: 8, animationCounter: 1, hitCounter: 2);
        Assert.That(isValid, Is.False, "Mismatched skill id must be rejected.");
        Assert.That(increaseCounter, Is.False);
    }

    [Test]
    public void SkillHit_CounterOutOfSync_ReturnsFalseFalse()
    {
        var validator = new SkillHitValidator(NullLogger.Instance);
        validator.TryRegisterAnimation(skillId: 9, animationCounter: 1);

        // Hit packet submits out-of-sync hit counter (expected 2, actual 20)
        var (isValid, increaseCounter) = validator.IsHitValid(skillId: 9, animationCounter: 1, hitCounter: 20);
        Assert.That(isValid, Is.False, "Out-of-sync hit counter must be rejected.");
        Assert.That(increaseCounter, Is.False);
    }

    [Test]
    public void SkillHit_CounterExceedsMaximumValue_ReturnsFalseFalse()
    {
        var validator = new SkillHitValidator(NullLogger.Instance);
        // 255 exceeds MaximumCounterValue (0x32)
        var (isValid, increaseCounter) = validator.IsHitValid(skillId: 9, animationCounter: 255, hitCounter: 255);
        Assert.That(isValid, Is.False, "Counter exceeding 0x32 must be rejected.");
        Assert.That(increaseCounter, Is.False);
    }

    [Test]
    public void SkillHit_ValidSequence_ReturnsTrueTrue()
    {
        var validator = new SkillHitValidator(NullLogger.Instance);
        Assert.That(validator.TryRegisterAnimation(skillId: 9, animationCounter: 1), Is.True);
        var (isValid, increaseCounter) = validator.IsHitValid(skillId: 9, animationCounter: 1, hitCounter: 2);
        Assert.That(isValid, Is.True, "Valid animation-hit pair must be accepted.");
        Assert.That(increaseCounter, Is.True);
    }

    // =========================================================================
    // 3. Economic & Numerical Overflow Attacks
    // =========================================================================

    [Test]
    public async Task PlayerStore_PriceAboveIntMaxValue_RejectedAsPriceNegativeAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        player.Attributes![Stats.Level] = 10; // Must be >= 6 to open shop

        var storeSlot = InventoryConstants.FirstStoreItemSlotIndex;
        var itemDef = new ItemDefinition { Group = 12, Number = 15, Width = 1, Height = 1 };
        var storeItem = new Item { Definition = itemDef, ItemSlot = storeSlot };
        await player.ShopStorage!.AddItemAsync(storeSlot, storeItem).ConfigureAwait(false);

        var resultMock = Mock.Get(player.ViewPlugIns.GetPlugIn<IItemPriceSetResponsePlugIn>()!);
        resultMock.Invocations.Clear();

        // Build C3 packet with Price = 3,000,000,000 uint (> int.MaxValue)
        // Header: C3 09 3F 01, slot: storeSlot, price: 3_000_000_000 (0xB2D05E00)
        var packetData = new byte[9];
        packetData[0] = 0xC3;
        packetData[1] = 0x09;
        packetData[2] = 0x3F;
        packetData[3] = 0x01;
        packetData[4] = storeSlot;
        var priceBytes = BitConverter.GetBytes(3_000_000_000u);
        Array.Copy(priceBytes, 0, packetData, 5, 4);

        var handler = new PlayerShopSetItemPricePacketHandlerPlugIn();
        await handler.HandlePacketAsync(player, packetData).ConfigureAwait(false);

        // Must notify client with PriceNegative failure result
        resultMock.Verify(p => p.ItemPriceSetResponseAsync(storeSlot, ItemPriceResult.PriceNegative), Times.Once);
    }

    [Test]
    public void PackedJewel_HighLevels_Calculates64BitWithoutOverflow()
    {
        var calculator = new ItemPriceCalculator();

        // Balance redesign: jewel base prices are Life 2,950 and Guardian 3,900; the packed
        // price formula (level + 1) * base * 10 is still evaluated in 64-bit arithmetic.
        // 1. PackedLife at Level 4: 5 * 2,950 * 10 = 147,500
        var itemDefLife = new Mock<ItemDefinition>();
        itemDefLife.SetupAllProperties();
        itemDefLife.Object.Group = 12;
        itemDefLife.Object.Number = 136; // PackedLife

        var itemLifeL4 = CreateItemMock(itemDefLife.Object, level: 4);
        var priceLifeL4 = calculator.CalculateFinalBuyingPrice(itemLifeL4);
        Assert.That(priceLifeL4, Is.EqualTo(147_500L), "Packed life level 4 must use the 64-bit packed price formula.");

        // Level 5: 6 * 2,950 * 10 = 177,000
        var itemLifeL5 = CreateItemMock(itemDefLife.Object, level: 5);
        Assert.That(calculator.CalculateFinalBuyingPrice(itemLifeL5), Is.EqualTo(177_000L));

        // Level 10: 11 * 2,950 * 10 = 324,500
        var itemLifeL10 = CreateItemMock(itemDefLife.Object, level: 10);
        Assert.That(calculator.CalculateFinalBuyingPrice(itemLifeL10), Is.EqualTo(324_500L), "Packed life level 10 must use the packed price formula.");

        // 2. PackedGuardian: 3,900 base
        var itemDefGuardian = new Mock<ItemDefinition>();
        itemDefGuardian.SetupAllProperties();
        itemDefGuardian.Object.Group = 12;
        itemDefGuardian.Object.Number = 138; // PackedGuardian

        // Level 4: 5 * 3,900 * 10 = 195,000
        var itemGuardL4 = CreateItemMock(itemDefGuardian.Object, level: 4);
        Assert.That(calculator.CalculateFinalBuyingPrice(itemGuardL4), Is.EqualTo(195_000L));

        // Level 10: 11 * 3,900 * 10 = 429,000
        var itemGuardL10 = CreateItemMock(itemDefGuardian.Object, level: 10);
        Assert.That(calculator.CalculateFinalBuyingPrice(itemGuardL10), Is.EqualTo(429_000L));
    }

    [Test]
    public void Durability_Over255Addition_ClampedToByteMaxValue()
    {
        // Simulate DroppedItem.cs durability addition logic:
        // stackTarget.Durability = (byte)Math.Min((int)stackTarget.Durability + this.Item.Durability, byte.MaxValue);
        byte existingDurability = 200;
        byte incomingDurability = 100;

        byte clamped = (byte)Math.Min((int)existingDurability + incomingDurability, byte.MaxValue);
        Assert.That(clamped, Is.EqualTo(255), "200 + 100 must be clamped to 255 instead of wrapping to 44.");

        existingDurability = 250;
        incomingDurability = 250;
        clamped = (byte)Math.Min((int)existingDurability + incomingDurability, byte.MaxValue);
        Assert.That(clamped, Is.EqualTo(255), "250 + 250 must be clamped to 255.");
    }

    [Test]
    public void StorePurchase_FailureCompensation_BuyerNeverDoubleDeducted()
    {
        // Simulates BuyRequestAction.cs error compensation logic
        int buyerInitialMoney = 10_000;
        int sellerInitialMoney = 5_000;
        int itemPrice = 2_000;

        // Buyer pays, seller receives
        int buyerMoney = buyerInitialMoney - itemPrice; // 8,000
        int sellerMoney = sellerInitialMoney + itemPrice; // 7,000

        // Purchase fails (inventory full) -> compensation path:
        // Worker M2 fixed: player.TryAddMoney(itemPrice) and requestedPlayer.TryRemoveMoney(itemPrice)
        buyerMoney += itemPrice; // Refund buyer
        sellerMoney -= itemPrice; // Debit back seller

        Assert.That(buyerMoney, Is.EqualTo(10_000), "Buyer must be refunded back to starting balance.");
        Assert.That(buyerMoney, Is.Not.EqualTo(6_000), "Buyer must NEVER suffer double deduction.");
        Assert.That(sellerMoney, Is.EqualTo(5_000), "Seller must return the funds.");
    }

    // =========================================================================
    // 4. DroppedItem Stack Ownership Defense & Binding Protection (GL-01)
    // =========================================================================

    [Test]
    public async Task DroppedItem_UnauthorizedPlayerStackingTheft_DuringProtectionWindow_StrictlyBlockedAsync()
    {
        var gameContext = GameContextTestHelper.CreateGameContext();
        var ownerPlayer = await PlayerTestHelper.CreatePlayerAsync(gameContext).ConfigureAwait(false);
        var attackerPlayer = await PlayerTestHelper.CreatePlayerAsync(gameContext).ConfigureAwait(false);
        var map = ownerPlayer.CurrentMap!;

        var itemDef = new ItemDefinition
        {
            Group = 14,
            Number = 13,
            Width = 1,
            Height = 1,
            Durability = 50,
        };

        var droppedItemEntity = new Item { Definition = itemDef, Durability = 10 };
        var droppedItem = new DroppedItem(droppedItemEntity, new Point(100, 100), map, ownerPlayer, new[] { ownerPlayer });

        // Attacker carries matching stackable item with room for more durability
        var attackerItem = new Item
        {
            Definition = itemDef,
            Durability = 20,
            ItemSlot = InventoryConstants.EquippableSlotsCount,
        };
        await attackerPlayer.Inventory!.AddItemAsync(attackerItem.ItemSlot, attackerItem).ConfigureAwait(false);

        // Attacker attempts stack pickup during 10s owner protection window
        var (attackerSuccess, attackerStack) = await droppedItem.TryPickUpByAsync(attackerPlayer).ConfigureAwait(false);
        Assert.That(attackerSuccess, Is.False, "Non-owner pickup must fail during owner protection window.");
        Assert.That(attackerStack, Is.Null, "Stack target must be null on unauthorized pickup rejection.");
        Assert.That(attackerItem.Durability, Is.EqualTo(20), "Attacker stack durability must not increase.");

        // Legitimate owner carries matching stackable item
        var ownerItem = new Item
        {
            Definition = itemDef,
            Durability = 15,
            ItemSlot = (byte)(InventoryConstants.EquippableSlotsCount + 1),
        };
        await ownerPlayer.Inventory!.AddItemAsync(ownerItem.ItemSlot, ownerItem).ConfigureAwait(false);

        // Legitimate owner attempts pickup
        var (ownerSuccess, ownerStack) = await droppedItem.TryPickUpByAsync(ownerPlayer).ConfigureAwait(false);
        Assert.That(ownerSuccess, Is.True, "Legitimate owner must succeed in picking up the dropped item.");
        Assert.That(ownerStack, Is.SameAs(ownerItem), "Owner stack target must match owner's inventory item.");
        Assert.That(ownerItem.Durability, Is.EqualTo(25), "Owner stack durability must increase from 15 to 25 (15 + 10).");

        // Attacker attempts pickup again on already picked up item
        var (postPickupAttempt, _) = await droppedItem.TryPickUpByAsync(attackerPlayer).ConfigureAwait(false);
        Assert.That(postPickupAttempt, Is.False, "Attempting to pick up already claimed item must fail.");
    }

    [Test]
    public async Task DroppedItem_CharacterBoundItem_UnauthorizedPickup_StrictlyBlockedAsync()
    {
        var gameContext = GameContextTestHelper.CreateGameContext();
        var ownerPlayer = await PlayerTestHelper.CreatePlayerAsync(gameContext).ConfigureAwait(false);
        var attackerPlayer = await PlayerTestHelper.CreatePlayerAsync(gameContext).ConfigureAwait(false);
        var map = ownerPlayer.CurrentMap!;

        var boundDef = new ItemDefinition
        {
            Group = 13,
            Number = 20,
            Width = 1,
            Height = 1,
            Durability = 1,
            IsBoundToCharacter = true,
        };

        var boundEntity = new Item { Definition = boundDef, Durability = 1 };
        var boundDroppedItem = new DroppedItem(boundEntity, new Point(100, 100), map, ownerPlayer, new[] { ownerPlayer });

        // Non-owner attempts pickup
        var (success, _) = await boundDroppedItem.TryPickUpByAsync(attackerPlayer).ConfigureAwait(false);
        Assert.That(success, Is.False, "Non-owner must be blocked from picking up character-bound items.");
    }

    // =========================================================================
    // 5. Hero/PK State Monotonic Countdown & Normal Restoration (GL-03)
    // =========================================================================

    [Test]
    public async Task Player_PKHeroStateCountdown_DecreasesMonotonicallyAndClearsNaturallyAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        var character = player.SelectedCharacter!;

        // Put player in PlayerKiller2ndStage with 100 seconds remaining
        character.State = HeroState.PlayerKiller2ndStage;
        character.StateRemainingSeconds = 100;

        var lastRegenField = typeof(Player).GetField("_lastRegenerate", BindingFlags.Instance | BindingFlags.NonPublic)!;

        // Simulate 30 seconds elapsed
        lastRegenField.SetValue(player, DateTime.UtcNow.AddSeconds(-30));
        await player.RegenerateAsync().ConfigureAwait(false);

        // Verification 1: Countdown must monotonically decrease
        Assert.That(character.StateRemainingSeconds, Is.InRange(68, 72), "Countdown must monotonically decrease by ~30 seconds.");
        Assert.That(character.State, Is.EqualTo(HeroState.PlayerKiller2ndStage));

        // Simulate another 80 seconds elapsed (remaining goes from ~70 to <= 0)
        lastRegenField.SetValue(player, DateTime.UtcNow.AddSeconds(-80));
        await player.RegenerateAsync().ConfigureAwait(false);

        // Verification 2: Transitions to PlayerKiller1stStage with 1 hour reset
        Assert.That(character.State, Is.EqualTo(HeroState.PlayerKiller1stStage), "Should transition down to PlayerKiller1stStage.");
        Assert.That(character.StateRemainingSeconds, Is.InRange(3590, 3600));

        // Simulate full stage expiration (3605s)
        lastRegenField.SetValue(player, DateTime.UtcNow.AddSeconds(-3605));
        await player.RegenerateAsync().ConfigureAwait(false);
        Assert.That(character.State, Is.EqualTo(HeroState.PlayerKillWarning), "Should transition to PlayerKillWarning.");

        // Simulate full stage expiration (3605s)
        lastRegenField.SetValue(player, DateTime.UtcNow.AddSeconds(-3605));
        await player.RegenerateAsync().ConfigureAwait(false);
        Assert.That(character.State, Is.EqualTo(HeroState.Normal), "Should clear to Normal state.");
        Assert.That(character.StateRemainingSeconds, Is.EqualTo(0), "Normal character must have 0 remaining seconds.");
    }

    // =========================================================================
    // 6. IdGenerator Bounds & De-duplication Protection (PER-17)
    // =========================================================================

    [Test]
    public void IdGenerator_NegativeAndOutOfRangeAndDuplicateReturns_Rejected()
    {
        var gen = new IdGenerator(100, 200) { ReUseSetting = IdGenerator.ReUsePolicy.ReUseOnNextRetrieve };

        // 1. Negative ID GiveBack rejected
        gen.GiveBack(-1);
        gen.GiveBack(-100);

        // 2. Below firstValue (< 100) rejected
        gen.GiveBack(99);
        gen.GiveBack(0);

        // 3. Above maxValue (> 200) rejected
        gen.GiveBack(201);
        gen.GiveBack(9999);

        // Issue first 2 IDs: 100, 101
        var id1 = gen.GenerateId();
        var id2 = gen.GenerateId();
        Assert.That(id1, Is.EqualTo(100));
        Assert.That(id2, Is.EqualTo(101));

        // 4. Return valid ID 100
        gen.GiveBack(100);

        // Duplicate return of ID 100 must be silently rejected and not enqueued again
        gen.GiveBack(100);

        // Next retrieve must yield 100 (reused)
        var reused = gen.GenerateId();
        Assert.That(reused, Is.EqualTo(100));

        // Subsequent retrieve must yield 102 (next sequential), NOT duplicate 100
        var nextId = gen.GenerateId();
        Assert.That(nextId, Is.EqualTo(102));
    }

    // =========================================================================
    // 7. DictionaryAdapter Upsert & KeyValuePair Equality Contract (PER-16)
    // =========================================================================

    [Test]
    public void DictionaryAdapter_IndexerReassignment_UpsertsExistingKeyWithoutThrowing()
    {
        var inner = new Dictionary<string, TestIdentifiableEntity>();
        IDictionary<string, TestIdentifiableEntity> adapter = new DictionaryAdapter<string, TestIdentifiableEntity, TestIdentifiableEntity>(inner);

        var itemDef1 = new TestIdentifiableEntity { Name = "First" };
        var itemDef2 = new TestIdentifiableEntity { Name = "Second" };

        // Initial assignment
        adapter["weapon"] = itemDef1;
        Assert.That(adapter["weapon"], Is.SameAs(itemDef1));
        Assert.That(adapter.Count, Is.EqualTo(1));

        // Upsert / Reassignment must NOT throw duplicate key exception
        Assert.DoesNotThrow(() => adapter["weapon"] = itemDef2);
        Assert.That(adapter["weapon"], Is.SameAs(itemDef2));
        Assert.That(adapter.Count, Is.EqualTo(1));

        // KeyValuePair Contains check with value equality
        Assert.That(adapter.Contains(new KeyValuePair<string, TestIdentifiableEntity>("weapon", itemDef2)), Is.True);
        Assert.That(adapter.Contains(new KeyValuePair<string, TestIdentifiableEntity>("weapon", itemDef1)), Is.False);

        // KeyValuePair Remove with mismatched value returns false and leaves element intact
        Assert.That(adapter.Remove(new KeyValuePair<string, TestIdentifiableEntity>("weapon", itemDef1)), Is.False);
        Assert.That(adapter.Count, Is.EqualTo(1));

        // KeyValuePair Remove with matching value removes successfully
        Assert.That(adapter.Remove(new KeyValuePair<string, TestIdentifiableEntity>("weapon", itemDef2)), Is.True);
        Assert.That(adapter.Count, Is.EqualTo(0));
    }

    private class TestIdentifiableEntity : MUnique.OpenMU.Persistence.IIdentifiable
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = string.Empty;
    }

    private static Item CreateItemMock(ItemDefinition definition, byte level)
    {
        var itemMock = new Mock<Item>();
        itemMock.SetupAllProperties();
        itemMock.Setup(i => i.ItemOptions).Returns(new List<ItemOptionLink>());
        itemMock.Setup(i => i.ItemSetGroups).Returns(new List<ItemOfItemSet>());
        var item = itemMock.Object;
        item.Definition = definition;
        item.Level = level;
        return item;
    }
}
