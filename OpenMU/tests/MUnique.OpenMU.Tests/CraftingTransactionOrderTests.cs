// <copyright file="CraftingTransactionOrderTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using Moq;
using MUnique.OpenMU.DataModel.Configuration.ItemCrafting;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.PlayerActions.Items;
using MUnique.OpenMU.GameLogic.Views.NPC;

/// <summary>
/// Tests for 70-01: synthesis must not charge/destroy materials before confirming
/// success, and any failure path (exception OR empty result) must refund the player
/// with no net asset loss.
/// </summary>
[TestFixture]
public class CraftingTransactionOrderTests
{
    private class ThrowingCraftingHandler : BaseItemCraftingHandler
    {
        private readonly int _price;

        public ThrowingCraftingHandler(int price = 1000)
        {
            this._price = price;
        }

        public Item? ConsumedItem { get; private set; }

        public override CraftingResult? TryGetRequiredItems(Player player, out IList<CraftingRequiredItemLink> items, out byte successRate)
        {
            successRate = 100;
            var storage = player.TemporaryStorage!;
            this.ConsumedItem = storage.Items.First();

            var req = new ItemCraftingRequiredItem
            {
                Reference = 1,
                MinimumAmount = 1,
                MaximumAmount = 1,
                SuccessResult = MixResult.Disappear,
                FailResult = MixResult.StaysAsIs,
            };

            items = new List<CraftingRequiredItemLink>
            {
                new(new List<Item> { this.ConsumedItem }, req),
            };
            return null;
        }

        protected override int GetPrice(byte successRate, IList<CraftingRequiredItemLink> requiredItems)
        {
            return this._price;
        }

        protected override async ValueTask<List<Item>> CreateOrModifyResultItemsAsync(
            IList<CraftingRequiredItemLink> requiredItems, Player player, byte socketSlot, byte successRate)
        {
            throw new InvalidOperationException("Simulated mid-synthesis failure");
        }
    }

    /// <summary>
    /// A crafting handler that returns an EMPTY list from CreateOrModifyResultItemsAsync
    /// (no exception, just no result produced). This is the corner case where the old
    /// code would still destroy materials and keep the zen.
    /// </summary>
    private class EmptyResultCraftingHandler : BaseItemCraftingHandler
    {
        private readonly int _price;

        public EmptyResultCraftingHandler(int price = 1000)
        {
            this._price = price;
        }

        public Item? ConsumedItem { get; private set; }

        public override CraftingResult? TryGetRequiredItems(Player player, out IList<CraftingRequiredItemLink> items, out byte successRate)
        {
            successRate = 100;
            var storage = player.TemporaryStorage!;
            this.ConsumedItem = storage.Items.First();

            var req = new ItemCraftingRequiredItem
            {
                Reference = 1,
                MinimumAmount = 1,
                MaximumAmount = 1,
                SuccessResult = MixResult.Disappear,
                FailResult = MixResult.StaysAsIs,
            };

            items = new List<CraftingRequiredItemLink>
            {
                new(new List<Item> { this.ConsumedItem }, req),
            };
            return null;
        }

        protected override int GetPrice(byte successRate, IList<CraftingRequiredItemLink> requiredItems)
        {
            return this._price;
        }

        protected override ValueTask<List<Item>> CreateOrModifyResultItemsAsync(
            IList<CraftingRequiredItemLink> requiredItems, Player player, byte socketSlot, byte successRate)
        {
            // Return an empty list: no result produced, no exception thrown.
            return ValueTask.FromResult(new List<Item>());
        }
    }

    [Test]
    public async Task MidSynthesisException_RefundsPriceAndKeepsItems()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);

        player.Money = 10000;
        var initialMoney = player.Money;

        var item = new Item
        {
            Definition = new MUnique.OpenMU.DataModel.Configuration.Items.ItemDefinition { Width = 1, Height = 1 },
            Durability = 1,
        };
        await player.TemporaryStorage!.AddItemAsync(item).ConfigureAwait(false);

        var handler = new ThrowingCraftingHandler(price: 1000);
        var (result, _) = await handler.DoMixAsync(player, socketSlot: 0).ConfigureAwait(false);

        Assert.That(result, Is.EqualTo(CraftingResult.Failed),
            "Mid-synthesis exception must result in CraftingResult.Failed, not an unhandled exception.");

        Assert.That(player.Money, Is.EqualTo(initialMoney),
            "Player money must be refunded to its original value after a mid-synthesis exception.");

        var remainingItems = player.TemporaryStorage!.Items.ToList();
        Assert.That(remainingItems, Has.Count.EqualTo(1),
            "Input items must not be destroyed when CreateOrModify throws.");
        Assert.That(remainingItems.First(), Is.SameAs(item),
            "The original item must still be present.");
    }

    [Test]
    public async Task EmptyResultProduced_RefundsPriceAndKeepsItems()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);

        player.Money = 10000;
        var initialMoney = player.Money;

        var item = new Item
        {
            Definition = new MUnique.OpenMU.DataModel.Configuration.Items.ItemDefinition { Width = 1, Height = 1 },
            Durability = 1,
        };
        await player.TemporaryStorage!.AddItemAsync(item).ConfigureAwait(false);

        var handler = new EmptyResultCraftingHandler(price: 1000);
        var (result, _) = await handler.DoMixAsync(player, socketSlot: 0).ConfigureAwait(false);

        Assert.That(result, Is.EqualTo(CraftingResult.Failed),
            "Empty result must return CraftingResult.Failed.");

        Assert.That(player.Money, Is.EqualTo(initialMoney),
            "Player money must be refunded when CreateOrModify produces an empty result.");

        var remainingItems = player.TemporaryStorage!.Items.ToList();
        Assert.That(remainingItems, Has.Count.EqualTo(1),
            "Input items must not be destroyed when CreateOrModify returns an empty list.");
        Assert.That(remainingItems.First(), Is.SameAs(item),
            "The original item must still be present.");
    }
}