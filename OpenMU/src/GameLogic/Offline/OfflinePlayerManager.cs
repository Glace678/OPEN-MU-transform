// <copyright file="OfflinePlayerManager.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Offline;

using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.MuHelper;
using MUnique.OpenMU.GameLogic.Views.Login;

/// <summary>
/// Manages active <see cref="OfflinePlayer"/> sessions.
/// </summary>
public sealed class OfflinePlayerManager
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, OfflinePlayer> _activePlayers =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets a snapshot of all currently active offline players.
    /// </summary>
    public IReadOnlyCollection<OfflinePlayer> OfflinePlayers
        => this._activePlayers.Values.ToList();

    /// <summary>
    /// Starts an offline player session by replacing the real player with a copy.
    /// </summary>
    /// <param name="realPlayer">The real player who typed the command.</param>
    /// <param name="loginName">The pre-validated account login name.</param>
    /// <returns><c>true</c> if the offline session was started successfully.</returns>
    public async ValueTask<bool> StartAsync(Player realPlayer, string loginName)
    {
        var characterName = realPlayer.SelectedCharacter?.Name;

        if (string.IsNullOrEmpty(characterName))
        {
            return false;
        }

        var sentinel = new OfflinePlayer(realPlayer.GameContext);

        // Atomically claim the slot to prevent racing during initialization.
        if (!this._activePlayers.TryAdd(loginName, sentinel))
        {
            await sentinel.DisposeAsync().ConfigureAwait(false);
            return false;
        }

        var initialCost = this.CalculateInitialZenCost(realPlayer);
        if (initialCost > 0 && !realPlayer.TryRemoveMoney(initialCost))
        {
            await this.RemoveAndDisposeAsync(loginName, sentinel).ConfigureAwait(false);
            return false;
        }

        var disconnectedRealPlayer = false;
        try
        {
            await this.TransitionToOfflineAsync(realPlayer, loginName).ConfigureAwait(false);
            disconnectedRealPlayer = true;

            if (!await sentinel.InitializeAsync(loginName, characterName).ConfigureAwait(false))
            {
                await RefundInitialZenCostAsync(realPlayer, loginName, characterName, initialCost, disconnectedRealPlayer).ConfigureAwait(false);
                await this.RemoveAndDisposeAsync(loginName, sentinel).ConfigureAwait(false);
                return false;
            }

            return true;
        }
        catch
        {
            await RefundInitialZenCostAsync(realPlayer, loginName, characterName, initialCost, disconnectedRealPlayer).ConfigureAwait(false);
            await this.RemoveAndDisposeAsync(loginName, sentinel).ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// Stops and removes the offline session for the given account if one exists.
    /// </summary>
    /// <param name="loginName">The account login name.</param>
    public async ValueTask StopAsync(string loginName)
    {
        if (!this._activePlayers.TryRemove(loginName, out var offlinePlayer))
        {
            // The session might have been started and stopped outside the manager (e.g. in tests).
            return;
        }

        try
        {
            await offlinePlayer.StopAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            offlinePlayer.Logger.LogError(ex, "Error stopping offline player session for {0}.", loginName);
        }
        finally
        {
            await offlinePlayer.GameContext.RemovePlayerAsync(offlinePlayer).ConfigureAwait(false);
            await offlinePlayer.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Returns whether an offline player session is currently active for <paramref name="loginName"/>.
    /// </summary>
    /// <param name="loginName">The account login name.</param>
    public bool IsActive(string loginName) => this._activePlayers.ContainsKey(loginName);

    /// <summary>
    /// Tries to get the active offline player for the given account login name.
    /// </summary>
    /// <param name="loginName">The account login name.</param>
    /// <param name="player">The offline player, if found.</param>
    /// <returns><c>true</c> if an active session exists; otherwise <c>false</c>.</returns>
    public bool TryGetPlayer(string loginName, out OfflinePlayer? player)
        => this._activePlayers.TryGetValue(loginName, out player);

    private async ValueTask TransitionToOfflineAsync(Player realPlayer, string loginName)
    {
        await this.LogOffFromLoginServerAsync(realPlayer, loginName).ConfigureAwait(false);

        // Send a close-game packet so the client exits cleanly without auto-reconnecting.
        // DisconnectAsync will then fire PlayerDisconnected, which triggers
        // RemovePlayerAsync (player list cleanup) and OnPlayerDisconnectedAsync (dispose).
        await realPlayer.InvokeViewPlugInAsync<ILogoutPlugIn>(p => p.LogoutAsync(LogoutType.CloseGame)).ConfigureAwait(false);

        await realPlayer.DisconnectAsync().ConfigureAwait(false);
    }

    private static async ValueTask RefundInitialZenCostAsync(Player realPlayer, string loginName, string characterName, int initialCost, bool disconnectedRealPlayer)
    {
        if (initialCost <= 0)
        {
            return;
        }

        if (!disconnectedRealPlayer && realPlayer.SelectedCharacter?.Inventory is not null && realPlayer.TryAddMoney(initialCost))
        {
            return;
        }

        try
        {
            using var context = realPlayer.GameContext.PersistenceContextProvider.CreateNewPlayerContext(realPlayer.GameContext.Configuration);
            var account = await context.GetAccountByLoginNameAsync(loginName).ConfigureAwait(false);
            var character = account?.Characters.FirstOrDefault(c => c.Name == characterName);
            if (character?.Inventory is { } inventory)
            {
                var maxMoney = realPlayer.GameContext.Configuration.MaximumInventoryMoney;
                inventory.Money = (int)Math.Min((long)inventory.Money + initialCost, maxMoney);
                await context.SaveChangesAsync().ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            realPlayer.Logger.LogError(ex, "Failed to refund offline initial Zen cost ({Cost}) for account '{LoginName}', character '{CharacterName}'.", initialCost, loginName, characterName);
        }
    }

    private int CalculateInitialZenCost(Player player)
    {
        var config = player.GameContext.FeaturePlugIns.GetPlugIn<MuHelperFeaturePlugIn>()?.Configuration
                     ?? new MuHelperConfiguration();

        var costPerStage = config.CostPerStage.FirstOrDefault();
        if (costPerStage <= 0)
        {
            return 0;
        }

        var totalLevel = player.Level + (int)(player.Attributes?[Stats.MasterLevel] ?? 0);

        return costPerStage * totalLevel;
    }

    /// <summary>
    /// Attempts to log the player off from the login server so the account slot is freed
    /// for reconnection while the ghost session is running. Failures are logged and swallowed
    /// because the offline session can still proceed without this step.
    /// </summary>
    /// <param name="player">The player being transitioned to offline.</param>
    /// <param name="loginName">The account login name.</param>
    private async ValueTask LogOffFromLoginServerAsync(Player player, string loginName)
    {
        if (player.GameContext is not IGameServerContext gsCtx)
        {
            return;
        }

        try
        {
            await gsCtx.LoginServer.LogOffAsync(loginName, gsCtx.Id).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            player.Logger.LogWarning(ex, "Could not log off from login server during offline player start.");
        }
    }

    /// <summary>
    /// Removes the sentinel entry from the active players dictionary and disposes the ghost.
    /// Used when startup fails before the ghost is fully initialized. The ghost is already part of the
    /// game context when the failure happened after <see cref="Player.SetSelectedCharacterAsync"/> was
    /// reached, and it is never disconnected on this path - the disposal takes it out of the player
    /// list (see <see cref="Player.DisposeAsyncCore"/>).
    /// </summary>
    /// <param name="loginName">The account login name.</param>
    /// <param name="sentinel">The ghost player to dispose.</param>
    private async ValueTask RemoveAndDisposeAsync(string loginName, OfflinePlayer sentinel)
    {
        this._activePlayers.TryRemove(loginName, out _);
        await sentinel.DisposeAsync().ConfigureAwait(false);
    }
}