// <copyright file="EnterQuestMapAction.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlayerActions;

using MUnique.OpenMU.GameLogic.PlayerActions.Quests;
using MUnique.OpenMU.GameLogic.Views.Inventory;

/// <summary>
/// Abstract base class for actions to enter maps which are only available when
/// in a specific quest.
/// </summary>
public abstract class EnterQuestMapAction
{
    private readonly byte _questGroup;
    private readonly short[] _questNumbers;

    private readonly short _npcNumber;
    private readonly int _price;
    private readonly short _targetMapNumber;

    /// <summary>
    /// Initializes a new instance of the <see cref="EnterQuestMapAction"/> class.
    /// </summary>
    /// <param name="npcNumber">The NPC number.</param>
    /// <param name="price">The price.</param>
    /// <param name="targetMapNumber">The target map number.</param>
    /// <param name="questGroup">The quest group.</param>
    /// <param name="questNumbers">The quest numbers.</param>
    protected EnterQuestMapAction(short npcNumber, int price, short targetMapNumber, byte questGroup, params short[] questNumbers)
    {
        this._questGroup = questGroup;
        this._questNumbers = questNumbers;
        this._npcNumber = npcNumber;
        this._price = price;
        this._targetMapNumber = targetMapNumber;
    }

    /// <summary>
    /// Tries the enter quest map.
    /// </summary>
    /// <param name="player">The player.</param>
    public async ValueTask TryEnterQuestMapAsync(Player player)
    {
        var openedNpc = player.OpenedNpc;
        if (openedNpc?.Definition?.Number != this._npcNumber)
        {
            player.Logger.LogWarning($"NPC {this._npcNumber} not opened.");
            return;
        }

        var activeQuestNumber = player.GetQuestState(this._questGroup)?.ActiveQuest?.Number;
        if (activeQuestNumber is null)
        {
            player.Logger.LogWarning("No quest is active.");
            return;
        }

        if (!this._questNumbers.Contains(activeQuestNumber.Value))
        {
            player.Logger.LogWarning($"Quest {activeQuestNumber} does not qualify to enter the map.");
            return;
        }

        var targetMap = player.GameContext.Configuration.Maps.FirstOrDefault(m => m.Number == this._targetMapNumber);
        if (targetMap is null)
        {
            player.Logger.LogError($"Map {this._targetMapNumber} wasn't found in the game configuration.");
            return;
        }

        var targetGate = targetMap.ExitGates.FirstOrDefault();
        if (targetGate is null)
        {
            player.Logger.LogError("Map {targetMap} has no exit gate", targetMap.Name.ValueInNeutralLanguage);
            return;
        }

        if (!player.IsAlive || player.CurrentMap is not { } currentMap)
        {
            return;
        }

        var originPosition = player.Position;
        var minQuestNumber = this._questNumbers.Length > 0 ? this._questNumbers.Min() : short.MaxValue;
        var eligiblePartyMembers = player.Party?.PartyList
            .OfType<Player>()
            .Where(p => !ReferenceEquals(p, player)
                && p.CurrentMap == currentMap
                && p.IsAlive
                && p.PlayerState.CurrentState == PlayerState.EnteredWorld
                && p.IsInRange(originPosition, 10)
                && this.IsQuestMapEligible(p, minQuestNumber))
            .ToList();

        var price = (int)SoloBalance.ScalePrice(this._price, player.GameContext.Configuration);
        if (price > 0 && !player.TryRemoveMoney(price))
        {
            player.Logger.LogError($"Not enough money to enter the map.");
            await player.ShowLocalizedBlueMessageAsync(nameof(PlayerMessage.NotEnoughMoneyToEnter), targetMap.Name.GetTranslation(player.Culture)).ConfigureAwait(false);
            return;
        }

        bool leaderWarped = false;
        try
        {
            leaderWarped = await player.TryWarpToAsync(targetGate).ConfigureAwait(false);
        }
        finally
        {
            if (!leaderWarped && price > 0)
            {
                player.TryAddMoney(price);
                await player.InvokeViewPlugInAsync<IUpdateMoneyPlugIn>(p => p.UpdateMoneyAsync()).ConfigureAwait(false);
            }
        }

        if (!leaderWarped || eligiblePartyMembers is null)
        {
            return;
        }

        foreach (var partyPlayer in eligiblePartyMembers)
        {
            if (partyPlayer.CurrentMap == currentMap
                && partyPlayer.IsAlive
                && partyPlayer.PlayerState.CurrentState == PlayerState.EnteredWorld)
            {
                await partyPlayer.TryWarpToAsync(targetGate).ConfigureAwait(false);
            }
        }
    }

    private bool IsQuestMapEligible(Player candidate, short minQuestNumber)
    {
        if (candidate.GetQuestState(this._questGroup) is not { } questState)
        {
            return false;
        }

        if (questState.ActiveQuest is { } activeQuest && this._questNumbers.Contains(activeQuest.Number))
        {
            return true;
        }

        return questState.LastFinishedQuest is { } finishedQuest && finishedQuest.Number >= minQuestNumber;
    }
}
