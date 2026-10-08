// <copyright file="TradeButtonAction.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlayerActions.Trade;

using MUnique.OpenMU.GameLogic.PlugIns;
using MUnique.OpenMU.GameLogic.Views.Trade;
using MUnique.OpenMU.Persistence;

/// <summary>
/// Action to change the trade button state.
/// </summary>
public class TradeButtonAction : BaseTradeAction
{
    /// <summary>
    /// Tries to change the trade button change to the new <paramref name="tradeButtonState"/>.
    /// </summary>
    /// <param name="trader">The trader.</param>
    /// <param name="tradeButtonState">The new state of the trade button.</param>
    public async ValueTask TradeButtonChangedAsync(ITrader trader, TradeButtonState tradeButtonState)
    {
        using var loggerScope = (trader as Player)?.Logger.BeginScope(this.GetType());
        var success = (tradeButtonState == TradeButtonState.Checked && await trader.PlayerState.TryAdvanceToAsync(PlayerState.TradeButtonPressed).ConfigureAwait(false))
                      || (tradeButtonState == TradeButtonState.Unchecked && await trader.PlayerState.TryAdvanceToAsync(PlayerState.TradeOpened).ConfigureAwait(false));
        if (!success)
        {
            return;
        }

        var tradingPartner = trader.TradingPartner;

        if (trader.PlayerState.CurrentState == PlayerState.TradeButtonPressed
            && tradingPartner is not null
            && tradingPartner.PlayerState.CurrentState == PlayerState.TradeButtonPressed)
        {
            TradeResult result = await this.InternalFinishTradeAsync(trader, tradingPartner).ConfigureAwait(false);
            if (result != TradeResult.Success)
            {
                await this.CancelTradeAsync(tradingPartner, false).ConfigureAwait(false);
                await this.CancelTradeAsync(trader, false).ConfigureAwait(false);
                (trader as Player)?.Logger.LogDebug($"Cancelled the trade because of unfinished state. trader: {trader.Name}, partner:{tradingPartner.Name}");
            }

            await trader.InvokeViewPlugInAsync<ITradeFinishedPlugIn>(p => p.TradeFinishedAsync(result)).ConfigureAwait(false);
            await tradingPartner.InvokeViewPlugInAsync<ITradeFinishedPlugIn>(p => p.TradeFinishedAsync(result)).ConfigureAwait(false);
        }
        else if (tradingPartner is not null)
        {
            await tradingPartner.InvokeViewPlugInAsync<IChangeTradeButtonStatePlugIn>(p => p.ChangeTradeButtonStateAsync(TradeButtonState.Checked)).ConfigureAwait(false);
        }
        else
        {
            // nothing to do.
        }
    }

    private static bool CanCreditTradeMoney(ITrader trader, int incomingMoney)
    {
        if (incomingMoney <= 0 || trader is not Player player)
        {
            return true;
        }

        var maximumMoney = player.GameContext?.Configuration?.MaximumInventoryMoney ?? int.MaxValue;
        return (long)player.Money + incomingMoney <= maximumMoney;
    }

    private static void CreditTradeMoney(ITrader trader, int incomingMoney)
    {
        if (incomingMoney == 0)
        {
            return;
        }

        if (trader is Player player)
        {
            // Validated up-front; this also updates the money view through the Player.Money setter.
            if (!player.TryAddMoney(incomingMoney))
            {
                throw new InvalidOperationException($"Could not credit {incomingMoney} zen to {player.Name}.");
            }
        }
        else
        {
            trader.Money += incomingMoney;
        }
    }

    private static async ValueTask<bool> TryAddItemsOfTradingPartnerAsync(ITrader trader)
    {
        if (trader.TradingPartner?.TemporaryStorage?.Items.Any() ?? false)
        {
            return await trader.Inventory!.TryTakeAllAsync(trader.TradingPartner.TemporaryStorage!).ConfigureAwait(false);
        }

        return true;
    }

    private async ValueTask<TradeResult> InternalFinishTradeAsync(ITrader trader, ITrader tradingPartner)
    {
        await using var context = await trader.PlayerState.TryBeginAdvanceToAsync(PlayerState.EnteredWorld).ConfigureAwait(false);
        await using var partnerContext = await tradingPartner.PlayerState.TryBeginAdvanceToAsync(PlayerState.EnteredWorld).ConfigureAwait(false);
        if (!context.Allowed || !partnerContext.Allowed)
        {
            context.Allowed = false;
            partnerContext.Allowed = false;
            (trader as Player)?.Logger.LogDebug($"Unexpected player states. {trader.Name}:{trader.PlayerState}, {tradingPartner.Name}:{tradingPartner.PlayerState}");
            return TradeResult.Cancelled;
        }

        using var itemContext = trader.GameContext.PersistenceContextProvider.CreateNewTradeContext();
        var traderItems = trader.TemporaryStorage!.Items.ToList();
        var tradePartnerItems = tradingPartner.TemporaryStorage!.Items.ToList();
        this.AttachItemsToPersistenceContext(traderItems, itemContext);
        this.AttachItemsToPersistenceContext(tradePartnerItems, itemContext);

        if (!await TryAddItemsOfTradingPartnerAsync(trader).ConfigureAwait(false) || !await TryAddItemsOfTradingPartnerAsync(tradingPartner).ConfigureAwait(false))
        {
            // TODO: Maybe notify which player has the full inventory.
            await this.SendMessageAsync(trader, nameof(PlayerMessage.InventoryFull)).ConfigureAwait(false);
            await this.SendMessageAsync(tradingPartner, nameof(PlayerMessage.InventoryFull)).ConfigureAwait(false);
            return TradeResult.FailedByFullInventory;
        }

        // The offered zen of each trader is already deducted (held in escrow). Verify
        // that crediting the partner's zen cannot exceed the money cap / overflow the
        // int before we persist anything, instead of wrapping it with a raw "+=" later.
        var traderIncoming = tradingPartner.TradingMoney;
        var partnerIncoming = trader.TradingMoney;
        if (!CanCreditTradeMoney(trader, traderIncoming) || !CanCreditTradeMoney(tradingPartner, partnerIncoming))
        {
            return TradeResult.Cancelled;
        }

        try
        {
            this.DetachItemsFromPersistenceContext(traderItems, trader.PersistenceContext);
            this.DetachItemsFromPersistenceContext(tradePartnerItems, trader.TradingPartner!.PersistenceContext);
            await itemContext.SaveChangesAsync().ConfigureAwait(false);
            this.AttachItemsToPersistenceContext(traderItems, trader.TradingPartner.PersistenceContext);
            this.AttachItemsToPersistenceContext(tradePartnerItems, trader.PersistenceContext);
            CreditTradeMoney(trader, traderIncoming);
            CreditTradeMoney(tradingPartner, partnerIncoming);

            // Durably settle both zen balances as part of the same operation as the
            // item transfer persisted above, so a crash cannot leave items delivered
            // without the corresponding money being persisted (P2).
            if (!await trader.SaveProgressAsync().ConfigureAwait(false)
                || !await tradingPartner.SaveProgressAsync().ConfigureAwait(false))
            {
                throw new InvalidOperationException("Persisting the traded zen balances failed.");
            }

            await trader.TradingPartner.InvokeViewPlugInAsync<IChangeTradeButtonStatePlugIn>(p => p.ChangeTradeButtonStateAsync(TradeButtonState.Checked)).ConfigureAwait(false);
            this.ResetTradeState(trader.TradingPartner);
            this.ResetTradeState(trader);
            this.CallPlugIn(traderItems, trader, tradingPartner);
            this.CallPlugIn(tradePartnerItems, tradingPartner, trader);
            FinishedTrades.Add(1);
            return TradeResult.Success;
        }
        catch (Exception exception)
        {
            await this.SendMessageAsync(trader, nameof(PlayerMessage.UnexpectedErrorDuringClosingTrade)).ConfigureAwait(false);
            await this.SendMessageAsync(tradingPartner, nameof(PlayerMessage.UnexpectedErrorDuringClosingTrade)).ConfigureAwait(false);
            context.Allowed = false;
            partnerContext.Allowed = false;
            (trader as Player)?.Logger.LogError(exception, $"An unexpected error occured during closing the trade. trader: {trader.Name}, partner:{tradingPartner.Name}");
            return TradeResult.Cancelled;
        }
    }

    private void CallPlugIn(IEnumerable<Item> items, ITrader source, ITrader target)
    {
        var point = target.GameContext.PlugInManager.GetPlugInPoint<IItemTradedToOtherPlayerPlugIn>();
        if (point is null)
        {
            return;
        }

        foreach (var item in items)
        {
            point.ItemTraded(source, target, item);
        }
    }

    private void AttachItemsToPersistenceContext(IEnumerable<Item> items, IContext itemContext)
    {
        foreach (var item in items)
        {
            itemContext.Attach(item);
        }
    }

    private void DetachItemsFromPersistenceContext(IEnumerable<Item> items, IContext itemContext)
    {
        foreach (var item in items)
        {
            itemContext.Detach(item);
        }
    }
}