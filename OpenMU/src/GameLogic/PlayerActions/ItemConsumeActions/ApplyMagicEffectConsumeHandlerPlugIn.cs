// <copyright file="ApplyMagicEffectConsumeHandlerPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlayerActions.ItemConsumeActions;

using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.GameLogic.Attributes;

/// <summary>
/// <see cref="IItemConsumeHandlerPlugIn"/> for <see cref="Item"/>s which have a defined <see cref="ItemDefinition.ConsumeEffect"/>.
/// </summary>
public class ApplyMagicEffectConsumeHandlerPlugIn : BaseConsumeHandlerPlugIn
{
    /// <inheritdoc />
    public override ItemIdentifier Key => new(0xFF, 0xFF);

    /// <inheritdoc />
    public override async ValueTask<bool> ConsumeItemAsync(Player player, Item item, Item? targetItem, FruitUsage fruitUsage)
    {
        if (item.Definition?.ConsumeEffect is not { } effectDefinition)
        {
            return false;
        }

        return await this.ConsumeItemCoreAsync(player, item, targetItem, fruitUsage, effectDefinition).ConfigureAwait(false);
    }

    /// <summary>
    /// Consumes the item at the specified slot with the specified effect and reduces its durability by one.
    /// If the durability has reached 0, the item is getting destroyed.
    /// If a target slot is specified, the consumption targets the item on this slot (e.g. upgrade of an item by a jewel).
    /// </summary>
    /// <param name="player">The player which is consuming.</param>
    /// <param name="item">The item which gets consumed.</param>
    /// <param name="targetItem">The item which is the target of the consumption (e.g. upgrade target of a jewel).</param>
    /// <param name="fruitUsage">In case the item is a fruit, this parameter defines how the fruit should be used.</param>
    /// <param name="effectDefinition">The effect definition.</param>
    /// <returns>
    /// The success of the consumption.
    /// </returns>
    protected async ValueTask<bool> ConsumeItemCoreAsync(Player player, Item item, Item? targetItem, FruitUsage fruitUsage, MagicEffectDefinition effectDefinition)
    {
        if (!this.CheckPreconditions(player, item)
            || player.Attributes is not { } attributes
            || !effectDefinition.PowerUpDefinitions.Any()
            || effectDefinition.Duration?.ConstantValue.Value is not { } durationInSeconds
            || !double.IsFinite(durationInSeconds)
            || durationInSeconds < 0.001
            || (double)durationInSeconds * 1000 > int.MaxValue)
        {
            return false;
        }

        // MagicEffect uses an int-millisecond timer. Validate before charging the
        // item or removing an existing effect, including during character selection.
        var duration = TimeSpan.FromSeconds(durationInSeconds);
        var boosts = effectDefinition.PowerUpDefinitions
            .Where(def => def.Boost is not null && def.TargetAttribute is not null)
            .Select(def => new MagicEffect.ElementWithTarget(attributes.CreateElement(def), def.TargetAttribute!))
            .ToArray();
        if (boosts.Length == 0)
        {
            return false;
        }

        if (!await base.ConsumeItemAsync(player, item, targetItem, fruitUsage).ConfigureAwait(false))
        {
            return false;
        }

        if (await player.MagicEffectList.TryGetActiveEffectOfSubTypeAsync(effectDefinition.SubType).ConfigureAwait(false) is { } existingEffect)
        {
            await existingEffect.DisposeAsync().ConfigureAwait(false);
        }

        var effect = new MagicEffect(duration, effectDefinition, boosts!);
        await player.MagicEffectList.AddEffectAsync(effect).ConfigureAwait(false);
        return true;
    }
}
