// <copyright file="GuildWarAnswerAction.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlayerActions.Guild;

using System.ComponentModel;
using System.Threading;
using MUnique.OpenMU.GameLogic.GuildWar;
using MUnique.OpenMU.GameLogic.Views.Guild;

/// <summary>
/// Action to handle the response of the requested guild master about the guild war.
/// </summary>
public class GuildWarAnswerAction
{
    /// <summary>
    /// Processes the answer.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="isWarAccepted">The answer.</param>
    public async ValueTask ProcessAnswerAsync(Player player, bool isWarAccepted)
    {
        if (player.GuildWarContext is not { } guildWarContext
            || guildWarContext.Requester is not { } requester)
        {
            await player.InvokeViewPlugInAsync<IShowShowGuildWarRequestResultPlugIn>(p => p.ShowResultAsync(GuildWarRequestResult.GuildNotFound)).ConfigureAwait(false);
            return;
        }

        if (guildWarContext.State != GuildWarState.Requested)
        {
            return;
        }

        SoccerGameMap? soccerMap = null;
        if (guildWarContext.WarType == GuildWarType.Soccer
            && player.GameContext.Configuration.Maps.FirstOrDefault(m => m.BattleZone?.Type == BattleType.Soccer) is { } definition)
        {
            soccerMap = (SoccerGameMap?)await player.GameContext.GetMapAsync(definition.Number.ToUnsigned()).ConfigureAwait(false);
        }

        var soccerInitFailed = false;
        if (guildWarContext.WarType == GuildWarType.Soccer && (soccerMap is null || soccerMap.IsBattleOngoing))
        {
            soccerInitFailed = true;
            await player.InvokeViewPlugInAsync<IShowShowGuildWarRequestResultPlugIn>(p => p.ShowResultAsync(GuildWarRequestResult.Failed)).ConfigureAwait(false);
        }

        GuildWarContext? requesterGuildWarContext;
        lock (GuildWarContext.SyncRoot)
        {
            if (!ReferenceEquals(player.GuildWarContext, guildWarContext)
                || guildWarContext.State != GuildWarState.Requested)
            {
                return;
            }

            requesterGuildWarContext = requester.GuildWarContext;
            var isValidRequesterContext = requesterGuildWarContext is { State: GuildWarState.Requested }
                && ReferenceEquals(requesterGuildWarContext.Score, guildWarContext.Score);
            if (!isWarAccepted || !isValidRequesterContext || soccerInitFailed)
            {
                guildWarContext.State = GuildWarState.Ended;
                player.GuildWarContext = null;
                if (isValidRequesterContext)
                {
                    requesterGuildWarContext!.State = GuildWarState.Ended;
                    requester.GuildWarContext = null;
                }

                return;
            }

            guildWarContext.State = GuildWarState.Started;
            requesterGuildWarContext!.State = GuildWarState.Started;
        }

        soccerMap?.InitializeBattle();
        var playerTeam = this.GetTeamPlayers(player);
        var requesterTeam = this.GetTeamPlayers(requester);
        var score = guildWarContext.Score;
        var playerGuildId = player.GuildStatus?.GuildId;
        var requesterGuildId = requester.GuildStatus?.GuildId;
        var playerTeamSide = guildWarContext.Team;
        var scoreAwarded = 0;
#pragma warning disable VSTHRD101 // Avoid unsupported async delegates
        score.PropertyChanged += async (_, args) =>
        {
            try
            {
                if (args.PropertyName != nameof(score.HasEnded)
                    || Interlocked.CompareExchange(ref scoreAwarded, 1, 0) != 0)
                {
                    return;
                }

                guildWarContext.State = GuildWarState.Ended;
                requesterGuildWarContext.State = GuildWarState.Ended;
                if (player.GameContext is IGameServerContext gameContext && score.Winners.HasValue)
                {
                    var winnerGuildId = score.Winners == playerTeamSide ? playerGuildId : requesterGuildId;
                    if (winnerGuildId is > 0)
                    {
                        await gameContext.GuildServer.IncreaseGuildScoreAsync(winnerGuildId.Value, 1).ConfigureAwait(false);
                    }
                }
            }
            catch (Exception ex)
            {
                // must be catched because it's async void.
                player.Logger.LogError(ex, "Unexpected error while finalizing guild war score.");
            }
        };
#pragma warning restore VSTHRD101 // Avoid unsupported async delegates

        foreach (var guildPlayer in playerTeam)
        {
            guildPlayer.GuildWarContext = guildWarContext;
            await guildPlayer.InvokeViewPlugInAsync<IShowGuildWarDeclaredPlugIn>(p => p.ShowDeclaredAsync()).ConfigureAwait(false);
            await guildPlayer.InvokeViewPlugInAsync<IGuildWarScoreUpdatePlugIn>(p => p.UpdateScoreAsync()).ConfigureAwait(false);
            RegisterScoreChangedEventWeakly(guildPlayer, score, soccerMap);
        }

        foreach (var guildPlayer in requesterTeam)
        {
            guildPlayer.GuildWarContext = requesterGuildWarContext;
            await guildPlayer.InvokeViewPlugInAsync<IShowGuildWarDeclaredPlugIn>(p => p.ShowDeclaredAsync()).ConfigureAwait(false);
            await guildPlayer.InvokeViewPlugInAsync<IGuildWarScoreUpdatePlugIn>(p => p.UpdateScoreAsync()).ConfigureAwait(false);
            RegisterScoreChangedEventWeakly(guildPlayer, score, soccerMap);
        }

        if (guildWarContext.WarType == GuildWarType.Soccer && soccerMap is { })
        {
            await this.MovePartyToArenaAsync(guildWarContext.Team, playerTeam, soccerMap).ConfigureAwait(false);
            await this.MovePartyToArenaAsync(requesterGuildWarContext.Team, requesterTeam, soccerMap).ConfigureAwait(false);
            await soccerMap.StartBattleAsync(score).ConfigureAwait(false);
        }
    }

    private static void RegisterScoreChangedEventWeakly(Player guildPlayer, GuildWarScore score, SoccerGameMap? soccerMap)
    {
        var playerReference = new WeakReference<Player>(guildPlayer);

#pragma warning disable VSTHRD100 // Avoid async void methods
        async void OnScorePropertyChanged(object? sender, PropertyChangedEventArgs args)
#pragma warning restore VSTHRD100 // Avoid async void methods
        {
            try
            {
                if (playerReference.TryGetTarget(out var p))
                {
                    await OnScoreChangedAsync(score, p, soccerMap, args).ConfigureAwait(false);
                }
                else
                {
                    score.PropertyChanged -= OnScorePropertyChanged;
                }
            }
            catch (Exception ex)
            {
                guildPlayer.Logger.LogError(ex, "Error handling a changed guild war score.");
            }
        }

        score.PropertyChanged += OnScorePropertyChanged;
    }

    private static async ValueTask OnScoreChangedAsync(GuildWarScore score, Player player, SoccerGameMap? soccerMap, PropertyChangedEventArgs args)
    {
        try
        {
            if (args.PropertyName == nameof(score.HasEnded))
            {
                if (player.GuildWarContext is { } context)
                {
                    var isWinner = context.Team == score.Winners;
                    await player.InvokeViewPlugInAsync<IShowGuildWarResultPlugIn>(p => p.ShowResultAsync(context.EnemyTeamName, isWinner ? GuildWarResult.Won : GuildWarResult.Lost)).ConfigureAwait(false);
                    if (soccerMap is not null)
                    {
                        var spawnGates = soccerMap.Definition.ExitGates.Where(g => g.IsSpawnGate);
                        if (spawnGates.Any())
                        {
                            await player.WarpToAsync(spawnGates.SelectRandom()!).ConfigureAwait(false);
                        }
                    }

                    player.GuildWarContext = null;
                }
            }
            else
            {
                await player.InvokeViewPlugInAsync<IGuildWarScoreUpdatePlugIn>(p => p.UpdateScoreAsync()).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            player.Logger.LogError(ex, "Unexpected error when notifying the player about a guild war score update");
        }
    }

    private ICollection<Player> GetTeamPlayers(Player guildMaster)
    {
        if (guildMaster.Party is { } party)
        {
            return party.PartyList.OfType<Player>().Where(p => p.GuildStatus?.GuildId == guildMaster.GuildStatus?.GuildId).ToList();
        }

        return new List<Player>(1) { guildMaster };
    }

    private async ValueTask MovePartyToArenaAsync(GuildWarTeam team, ICollection<Player> members, SoccerGameMap soccerMap)
    {
        var ground = soccerMap.Definition.BattleZone?.Ground;
        if (ground is null)
        {
            return;
        }

        var increaseX = soccerMap.Definition.BattleZone?.LeftTeamSpawnPointX is not null;

        var exitGate = new ExitGate
        {
            X1 = (team == GuildWarTeam.First ? soccerMap.Definition.BattleZone?.LeftTeamSpawnPointX : soccerMap.Definition.BattleZone?.RightTeamSpawnPointX) ?? ground.X1,
            X2 = (team == GuildWarTeam.First ? soccerMap.Definition.BattleZone?.LeftTeamSpawnPointX : soccerMap.Definition.BattleZone?.RightTeamSpawnPointX) ?? ground.X2,
            Y1 = (team == GuildWarTeam.First ? soccerMap.Definition.BattleZone?.LeftTeamSpawnPointY : soccerMap.Definition.BattleZone?.RightTeamSpawnPointY) ?? ground.Y1,
            Y2 = (team == GuildWarTeam.First ? soccerMap.Definition.BattleZone?.LeftTeamSpawnPointY : soccerMap.Definition.BattleZone?.RightTeamSpawnPointY) ?? ground.Y2,
            Map = soccerMap.Definition,
        };

        foreach (var member in members)
        {
            await member.WarpToAsync(exitGate).ConfigureAwait(false);
            if (increaseX)
            {
                exitGate.X1++;
                exitGate.X2++;
            }
        }
    }
}
