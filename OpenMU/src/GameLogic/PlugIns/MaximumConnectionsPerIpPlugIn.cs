// <copyright file="MaximumConnectionsPerIpPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns;

using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// A plugin that limits the number of simultaneous active player sessions connected from the same IP address.
/// </summary>
[PlugIn]
[Display(Name = "Maximum Connections Per IP", Description = "Limits the maximum number of parallel connections from the same IP address.")]
[Guid("2C779F5E-379E-4CE0-BFCC-CA6455D757B3")]
public class MaximumConnectionsPerIpPlugIn : IPlayerStateChangingPlugIn, IPlayerStateChangedPlugIn, ISupportCustomConfiguration<MaximumConnectionsPerIpPlugInConfiguration>, ISupportDefaultCustomConfiguration, IDisabledByDefault
{
    private static readonly TimeSpan ReservationTimeout = TimeSpan.FromSeconds(15);

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly List<(WeakReference<Player> PlayerRef, IGameContext Context, string IpAddress, DateTime ReservedAtUtc)> _pendingAdmissions = [];

    /// <inheritdoc />
    public MaximumConnectionsPerIpPlugInConfiguration? Configuration { get; set; }

    /// <inheritdoc />
    public async ValueTask PlayerStateChangingAsync(Player player, StateMachine.StateChangeEventArgs eventArgs)
    {
        if (eventArgs.NextState != PlayerState.Authenticated)
        {
            return;
        }

        var rawIpAddress = (player as IHasIpAddress)?.IpAddress;
        if (string.IsNullOrWhiteSpace(rawIpAddress))
        {
            return;
        }

        var normalizedIp = NormalizeIpAddress(rawIpAddress);
        var config = this.Configuration ?? CreateDefaultConfiguration();
        await this._gate.WaitAsync().ConfigureAwait(false);
        try
        {
            var now = DateTime.UtcNow;
            this.PrunePendingAdmissionsUnsafe(now);

            var players = await player.GameContext.GetPlayersAsync().ConfigureAwait(false);
            var activeSameIpCount = players.Count(p =>
                !ReferenceEquals(p, player)
                && p.PlayerState.CurrentState != PlayerState.Initial
                && p.PlayerState.CurrentState != PlayerState.LoginScreen
                && !p.PlayerState.CurrentState.IsDisconnectedOrFinished()
                && NormalizeIpAddress((p as IHasIpAddress)?.IpAddress) == normalizedIp);

            var pendingSameIpCount = this._pendingAdmissions.Count(entry =>
                ReferenceEquals(entry.Context, player.GameContext)
                && entry.IpAddress == normalizedIp
                && entry.PlayerRef.TryGetTarget(out var pendingPlayer)
                && !ReferenceEquals(pendingPlayer, player)
                && pendingPlayer.PlayerState.CurrentState == PlayerState.LoginScreen
                && players.All(active => !ReferenceEquals(active, pendingPlayer) || active.PlayerState.CurrentState == PlayerState.LoginScreen));

            if (activeSameIpCount + pendingSameIpCount >= config.MaximumConnectionsPerIp)
            {
                player.Logger.LogWarning(
                    "Login request for IP '{IpAddress}' was cancelled. It exceeded the maximum connection limit of {Limit}.",
                    normalizedIp,
                    config.MaximumConnectionsPerIp);
                player.LoginResultOverride = Views.Login.LoginResult.ServerIsFull;
                eventArgs.Cancel = true;
                return;
            }

            this._pendingAdmissions.Add((new WeakReference<Player>(player), player.GameContext, normalizedIp, now));
        }
        finally
        {
            this._gate.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask PlayerStateChangedAsync(Player player, State previousState, State currentState)
    {
        if (currentState != PlayerState.Authenticated && !currentState.IsDisconnectedOrFinished() && currentState != PlayerState.LoginScreen)
        {
            return;
        }

        await this._gate.WaitAsync().ConfigureAwait(false);
        try
        {
            this._pendingAdmissions.RemoveAll(entry => !entry.PlayerRef.TryGetTarget(out var target) || ReferenceEquals(target, player));
        }
        finally
        {
            this._gate.Release();
        }
    }

    /// <inheritdoc />
    public object CreateDefaultConfig()
    {
        return CreateDefaultConfiguration();
    }

    private static string NormalizeIpAddress(string? ipAddress)
    {
        if (string.IsNullOrWhiteSpace(ipAddress))
        {
            return string.Empty;
        }

        var trimmed = ipAddress.Trim();
        if (IPAddress.TryParse(trimmed, out var parsed))
        {
            if (parsed.IsIPv4MappedToIPv6)
            {
                parsed = parsed.MapToIPv4();
            }

            return parsed.ToString();
        }

        return trimmed;
    }

    private static MaximumConnectionsPerIpPlugInConfiguration CreateDefaultConfiguration()
    {
        return new MaximumConnectionsPerIpPlugInConfiguration();
    }

    private void PrunePendingAdmissionsUnsafe(DateTime nowUtc)
    {
        this._pendingAdmissions.RemoveAll(entry =>
            nowUtc - entry.ReservedAtUtc > ReservationTimeout
            || !entry.PlayerRef.TryGetTarget(out var target)
            || target.PlayerState.CurrentState != PlayerState.LoginScreen);
    }
}
