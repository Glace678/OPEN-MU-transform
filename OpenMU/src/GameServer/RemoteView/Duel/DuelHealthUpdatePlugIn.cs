// <copyright file="DuelHealthUpdatePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView.Duel;

using System.Runtime.InteropServices;
using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.Views;
using MUnique.OpenMU.GameLogic.Views.Duel;
using MUnique.OpenMU.Network.Packets.ServerToClient;
using MUnique.OpenMU.Network.PlugIns;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// The default implementation of the <see cref="IDuelHealthUpdatePlugIn"/> which is forwarding everything to the game client with specific data packets.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.DuelHealthUpdatePlugIn_Name), Description = nameof(PlugInResources.DuelHealthUpdatePlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("40CE7F73-F9DF-4F4E-BBCE-04938604A72C")]
[MinimumClient(4, 0, ClientLanguage.Invariant)]
public class DuelHealthUpdatePlugIn : IDuelHealthUpdatePlugIn
{
    private readonly RemotePlayer _player;

    /// <summary>
    /// Initializes a new instance of the <see cref="DuelHealthUpdatePlugIn"/> class.
    /// </summary>
    /// <param name="player">The player.</param>
    public DuelHealthUpdatePlugIn(RemotePlayer player) => this._player = player;

    /// <inheritdoc />
    public async ValueTask UpdateHealthAsync(DuelRoom duelRoom)
    {
        var player1 = this._player == duelRoom.Opponent ? duelRoom.Opponent : duelRoom.Requester;
        var player2 = this._player == duelRoom.Opponent ? duelRoom.Requester : duelRoom.Opponent;
        if (player1.Attributes is null || player2.Attributes is null)
        {
            return;
        }

        var player1Health = GetPercentage(player1.Attributes, Stats.CurrentHealth, Stats.MaximumHealth);
        var player1Shield = GetPercentage(player1.Attributes, Stats.CurrentShield, Stats.MaximumShield);
        var player2Health = GetPercentage(player2.Attributes, Stats.CurrentHealth, Stats.MaximumHealth);
        var player2Shield = GetPercentage(player2.Attributes, Stats.CurrentShield, Stats.MaximumShield);

        await this._player.Connection.SendDuelHealthUpdateAsync(
                player1.GetId(this._player),
                player2.GetId(this._player),
                player1Health,
                player2Health,
                player1Shield,
                player2Shield)
            .ConfigureAwait(false);
    }

    private static byte GetPercentage(IAttributeSystem attributes, AttributeDefinition current, AttributeDefinition maximum)
    {
        var max = attributes[maximum];
        if (max <= 0f)
        {
            return 0;
        }

        return (byte)Math.Clamp((attributes[current] * 100f) / max, 0f, 100f);
    }
}