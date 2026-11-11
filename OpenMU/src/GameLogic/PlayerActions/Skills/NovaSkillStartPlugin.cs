// <copyright file="NovaSkillStartPlugin.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlayerActions.Skills;

using System.Runtime.InteropServices;
using System.Threading;
using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.GuildWar;
using MUnique.OpenMU.GameLogic.NPC;
using MUnique.OpenMU.GameLogic.Views.World;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// The start nova skill action.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.NovaSkillStartPlugin_Name), Description = nameof(PlugInResources.NovaSkillStartPlugin_Description), ResourceType = typeof(PlugInResources))]
[Guid("e966e7eb-58b8-4356-8725-5da9f43c1fa4")]
public class NovaSkillStartPlugin : TargetedSkillPluginBase
{
    private const ushort NovaEndSkillId = 40;

    private static readonly TimeSpan NovaStepDelay = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// The nova damage per stage, which is still hardcoded. May be configurable later.
    /// </summary>
    private static readonly int[] NovaDamageTable = { 0, 20, 50, 99, 160, 225, 325, 425, 550, 700, 880, 1090, 1320 };

    /// <inheritdoc/>
    public override short Key => 58;

    /// <inheritdoc />
    public override async ValueTask PerformSkillAsync(Player player, IAttackable target, ushort skillId)
    {
        if (!player.IsAlive || player.IsAtSafezone() || player.Attributes is not { } attributes)
        {
            return;
        }

        var skillEntry = player.SkillList?.GetSkill(NovaEndSkillId);
        if (skillEntry?.Skill is null)
        {
            return;
        }

        var novaStart = player.GameContext.Configuration.Skills.FirstOrDefault(s => s.Number == skillId);
        if (novaStart is null)
        {
            return;
        }

        if (novaStart.ConsumeRequirements.Any(r => player.GetRequiredValue(r) > attributes[r.Attribute]))
        {
            return;
        }

        var cancellationTokenSource = new SkillCancellationTokenSource();
        if (!player.TryClaimSkillCancelTokenSource(cancellationTokenSource))
        {
            // A channeled skill is already ongoing.
            cancellationTokenSource.Dispose();
            return;
        }

        try
        {
            if (novaStart.ConsumeRequirements.Any(r => player.GetRequiredValue(r) > attributes[r.Attribute]))
            {
                player.ReleaseSkillCancelTokenSource(cancellationTokenSource);
                return;
            }

            foreach (var requirement in novaStart.ConsumeRequirements)
            {
                attributes[requirement.Attribute] -= player.GetRequiredValue(requirement);
            }

            await player.ForEachWorldObserverAsync<IShowSkillAnimationPlugIn>(p => p.ShowNovaStartAsync(player), true).ConfigureAwait(false);
        }
        catch
        {
            player.ReleaseSkillCancelTokenSource(cancellationTokenSource);
            throw;
        }

        _ = Task.Run(() => this.RunNovaAsync(player, skillEntry, cancellationTokenSource));
    }

    private async ValueTask RunNovaAsync(Player player, SkillEntry skillEntry, SkillCancellationTokenSource cancellationTokenSource)
    {
        var cancellationToken = cancellationTokenSource.Token;
        SimpleElement? stepDamageElement = null;
        try
        {
            if (player.Attributes is not { } playerAttributes
                || skillEntry.Skill is null)
            {
                return;
            }

            byte completedSteps = 0;

            stepDamageElement = new SimpleElement(0, AggregateType.AddRaw);
            playerAttributes.AddElement(stepDamageElement, Stats.NovaStageDamage);
            try
            {
                while (completedSteps < NovaDamageTable.Length - 1)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (!player.IsAlive || player.IsAtSafezone() || !await player.TryConsumeForSkillAsync(skillEntry).ConfigureAwait(false))
                    {
                        break;
                    }

                    completedSteps++;
                    stepDamageElement.Value = NovaDamageTable[completedSteps];
                    var steps = completedSteps;
                    await player.ForEachWorldObserverAsync<IShowSkillStageUpdatePlugIn>(p => p.UpdateSkillStageAsync(player, this.Key, steps), true).ConfigureAwait(false);
                    await Task.Delay(NovaStepDelay, cancellationToken).ConfigureAwait(false); // Hint: Player could cancel the nova 500 ms before end without damage loss - if he is good
                }
            }
            catch (OperationCanceledException)
            {
                // This is expected when the player stopped nova before.
            }

            await this.AttackTargetsAsync(player, skillEntry, cancellationTokenSource).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            player.Logger.LogError(ex, "Unexpected error during performing nova skill");
        }
        finally
        {
            if (stepDamageElement is not null)
            {
                player.Attributes?.RemoveElement(stepDamageElement, Stats.NovaStageDamage);
            }

            player.ReleaseSkillCancelTokenSource(cancellationTokenSource);
        }
    }

    private async ValueTask AttackTargetsAsync(Player player, SkillEntry skillEntry, SkillCancellationTokenSource cancellationTokenSource)
    {
        if (!player.IsAlive || player.IsAtSafezone() || player.CurrentMap is not { } expectedMap || skillEntry.Skill is not { } skill)
        {
            return;
        }

        await player.ForEachWorldObserverAsync<IShowSkillAnimationPlugIn>(p => p.ShowSkillAnimationAsync(player, player, skill.Number, true), true).ConfigureAwait(false);
        var explicitTargetId = cancellationTokenSource.ExplicitTargetId;
        var targets = this.DetermineTargets(player, skill, explicitTargetId).ToList();

        await Task.Delay(500, CancellationToken.None).ConfigureAwait(false);

        if (!player.IsAlive || !player.IsActive() || player.IsAtSafezone() || player.CurrentMap != expectedMap)
        {
            return;
        }

        foreach (var target in targets)
        {
            if (target.IsAlive && target.IsActive() && !target.IsAtSafezone() && target.CurrentMap == expectedMap)
            {
                await target.AttackByAsync(player, skillEntry, false).ConfigureAwait(false);
            }
        }
    }

    private IEnumerable<IAttackable> DetermineTargets(Player player, Skill skill, ushort? explicitTargetId)
    {
        bool FilterPlayer(Player attackable)
        {
            if (attackable == player || attackable.IsAtSafezone())
            {
                // Don't attack yourself
                return false;
            }

            if (attackable.Id == explicitTargetId)
            {
                return true;
            }

            if (player.GuildWarContext is { State: GuildWarState.Started } attackerContext
                && attackable.GuildWarContext is { State: GuildWarState.Started } defenderContext
                && attackerContext.Team != defenderContext.Team
                && attackerContext.Score == defenderContext.Score)
            {
                return true;
            }

            // todo: during castle siege, nova attacks everyone
            // todo: during duel, it always attacks the opponent
            return false;
        }

        bool FilterMonster(Monster monster)
        {
            if (monster.Id == explicitTargetId)
            {
                return true;
            }

            if (monster.SummonedBy is not { } summonedBy)
            {
                // attack all monsters which are not summoned by a player.
                return true;
            }

            return FilterPlayer(summonedBy);
        }

        var targets = player.CurrentMap?
            .GetAttackablesInRange(player.Position, skill.Range)
            .Where(a => a.IsAlive && !a.IsAtSafezone())
            .Where(a => a is not Monster || FilterMonster((Monster)a))
            .Where(a => a is not Player || FilterPlayer((Player)a))
            ?? [];

        return targets;
    }
}