// <copyright file="DuelRoomManager.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic;

using System.Threading;
using MUnique.OpenMU.GameLogic.Views.Duel;
using Nito.AsyncEx;

/// <summary>
/// A class that manages several instances of <see cref="DuelRoom"/>.
/// </summary>
public class DuelRoomManager
{
    private readonly DuelConfiguration _configuration;
    private readonly AsyncLock _lock = new AsyncLock();
    private readonly DuelRoom?[] _duelRooms;

    /// <summary>
    /// Initializes a new instance of the <see cref="DuelRoomManager"/> class.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    public DuelRoomManager(DuelConfiguration configuration)
    {
        this._configuration = configuration;
        this._duelRooms = new DuelRoom?[this.MaxRoomCount];
    }

    /// <summary>
    /// Gets the maximum <see cref="DuelRoom"/>s count.
    /// </summary>
    public int MaxRoomCount => this._configuration?.DuelAreas.Count ?? 0;

    /// <summary>
    /// Gets the time after which a duel request without a response is considered expired.
    /// </summary>
    public static TimeSpan RequestTimeout => TimeSpan.FromSeconds(60);

    /// <summary>
    /// Gets a <see cref="DuelRoom"/> for the two duelist players.
    /// </summary>
    /// <param name="player1">The first player.</param>
    /// <param name="player2">The second player.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The value task with a free <see cref="DuelRoom"/>.</returns>
    public async ValueTask<DuelRoom?> GetFreeDuelRoomAsync(Player player1, Player player2, CancellationToken cancellationToken = default)
    {
        DuelRoom? timedOutRoom = null;
        var l = await this._lock.LockAsync(cancellationToken);
        try
        {
            for (int i = 0; i < this._duelRooms.Length; i++)
            {
                var room = this._duelRooms[i];
                if (room is null)
                {
                    var area = this._configuration.DuelAreas.First(a => a.Index == i);

                    return this._duelRooms[i] = new DuelRoom(area, player1, player2);
                }

                if (timedOutRoom is null
                    && room.State is DuelState.DuelRequested
                    && DateTime.UtcNow - room.CreatedAt > RequestTimeout)
                {
                    // Free the slot now; the room is disposed after the lock is released below.
                    timedOutRoom = room;
                    this._duelRooms[i] = null;
                }
            }
        }
        finally
        {
            l.Dispose();
        }

        if (timedOutRoom is null)
        {
            return null;
        }

        await timedOutRoom.ResetAndDisposeAsync(DuelStartResult.Refused).ConfigureAwait(false);
        return await this.GetFreeDuelRoomAsync(player1, player2, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Discards a <see cref="DuelRoom"/>.
    /// </summary>
    /// <param name="duelRoom">The <see cref="DuelRoom"/>.</param>
    /// <returns>A <see cref="ValueTask"/>.</returns>
    public async ValueTask GiveBackDuelRoomAsync(DuelRoom duelRoom)
    {
        using var l = await this._lock.LockAsync();
        if (this._duelRooms[duelRoom.Index] == duelRoom)
        {
            // Only clear the slot if it still holds this room. A timed-out room may already
            // have been removed and its slot could be reused by a newer room.
            this._duelRooms[duelRoom.Index] = null;
        }
    }

    /// <summary>
    /// Gets a <see cref="DuelRoom"/> by its index number.
    /// </summary>
    /// <param name="requestedDuelIndex">The index of the <see cref="DuelRoom"/>.</param>
    /// <returns>A <see cref="DuelRoom"/>.</returns>
    public DuelRoom? GetRoomByIndex(byte requestedDuelIndex)
    {
        if (this.MaxRoomCount <= requestedDuelIndex)
        {
            return null;
        }

        return this._duelRooms[requestedDuelIndex];
    }

    /// <summary>
    /// Shows the <see cref="DuelRoom"/>s to a player.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <returns>A <see cref="ValueTask"/>.</returns>
    public async ValueTask ShowRoomsAsync(Player player)
    {
        await player.InvokeViewPlugInAsync<IDuelStatusUpdatePlugIn>(p => p.UpdateStatusAsync(this._duelRooms)).ConfigureAwait(false);
    }
}