// -----------------------------------------------------------------------
// <copyright file="UpgradeItemLevelJewelConsumeHandlerPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace MUnique.OpenMU.GameLogic.PlayerActions.ItemConsumeActions;

using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Base class for consume handlers which upgrade the item level by consuming a jewel.
/// </summary>
/// <typeparam name="TConfig">The type of the configuration.</typeparam>
public abstract class UpgradeItemLevelJewelConsumeHandlerPlugIn<TConfig>
    : ItemModifyConsumeHandlerPlugIn, ISupportCustomConfiguration<TConfig>, ISupportDefaultCustomConfiguration
    where TConfig : UpgradeItemLevelConfiguration
{
    private readonly IRandomizer _randomizer;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpgradeItemLevelJewelConsumeHandlerPlugIn{TConfig}"/> class.
    /// </summary>
    protected UpgradeItemLevelJewelConsumeHandlerPlugIn()
        : this(Rand.GetRandomizer())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="UpgradeItemLevelJewelConsumeHandlerPlugIn{TConfig}"/> class.
    /// </summary>
    /// <param name="randomizer">The randomizer.</param>
    protected UpgradeItemLevelJewelConsumeHandlerPlugIn(IRandomizer randomizer)
    {
        this._randomizer = randomizer;
    }

    /// <inheritdoc/>
    public TConfig? Configuration { get; set; }

    /// <inheritdoc />
    public abstract object CreateDefaultConfig();

    /// <inheritdoc/>
    protected override bool ModifyItem(Player player, Item item)
    {
        return BalanceV1.IsEnabled(player.GameContext.Configuration)
            ? this.ModifyItemCore(item, useBalanceV1Rules: true)
            : this.ModifyItem(item, player.PersistenceContext);
    }

    /// <inheritdoc/>
    protected override bool ModifyItem(Item item, IContext persistenceContext)
    {
        return this.ModifyItemCore(item, useBalanceV1Rules: false);
    }

    private bool ModifyItemCore(Item item, bool useBalanceV1Rules)
    {
        if (!item.CanLevelBeUpgraded())
        {
            return false;
        }

        this.Configuration ??= (TConfig)this.CreateDefaultConfig();
        if (item.Level < this.Configuration.MinimumLevel)
        {
            return false;
        }

        if (this.Configuration.DisallowedItems.Contains(item.Definition!))
        {
            return false;
        }

        if (this.Configuration.AllowedItems.Any() && !this.Configuration.AllowedItems.Contains(item.Definition!))
        {
            return false;
        }

        var maximumAllowedLevel = Math.Min(this.Configuration.MaximumLevel + 1, item.Definition!.MaximumItemLevel);
        var levelAmount = Math.Min(this.Configuration.LevelAmount, maximumAllowedLevel - item.Level);
        if (levelAmount <= 0)
        {
            return false;
        }

        int percent;
        if (useBalanceV1Rules)
        {
            percent = checked((int)Math.Round(BalanceV1.GetUpgradeStep(item.Level + levelAmount).Chance * 100));
        }
        else
        {
            percent = this.Configuration.SuccessRatePercentage;
            if (ItemHasLuck(item))
            {
                percent += this.Configuration.SuccessRateBonusWithLuckPercentage;
            }
        }

        if (this._randomizer.NextRandomBool(percent))
        {
            item.Level += (byte)levelAmount;
            item.Durability = item.GetMaximumDurabilityOfOnePiece();
            return true; // true doesn't mean that it was successful, just that the consumption happened.
        }

        if (useBalanceV1Rules)
        {
            return true;
        }

        if (item.Level >= this.Configuration.ResetToLevel0WhenFailMinLevel)
        {
            item.Level = 0;
        }
        else
        {
            item.Level = (byte)Math.Max(item.Level - 1, 0);
        }

        return true;
    }

    private static bool ItemHasLuck(Item item)
    {
        return item.ItemOptions.Any(o => o.ItemOption?.OptionType == ItemOptionTypes.Luck);
    }
}
