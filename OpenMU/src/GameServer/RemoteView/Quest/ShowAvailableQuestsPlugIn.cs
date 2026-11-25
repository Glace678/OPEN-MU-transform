// <copyright file="ShowAvailableQuestsPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView.Quest;

using System.Linq;

using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic.PlayerActions.Quests;
using MUnique.OpenMU.GameLogic.Views.Quest;
using MUnique.OpenMU.Network;
using MUnique.OpenMU.Network.Packets.ServerToClient;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// The default implementation of the <see cref="IShowAvailableQuestsPlugIn"/> which is forwarding everything to the game client with specific data packets.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.ShowAvailableQuestsPlugIn_Name), Description = nameof(PlugInResources.ShowAvailableQuestsPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("63C7B3E2-EF66-49BB-A9F8-EFBD2389588F")]
public class ShowAvailableQuestsPlugIn : IShowAvailableQuestsPlugIn
{
    private readonly RemotePlayer _player;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShowAvailableQuestsPlugIn"/> class.
    /// </summary>
    /// <param name="player">The player.</param>
    public ShowAvailableQuestsPlugIn(RemotePlayer player)
    {
        this._player = player;
    }

    /// <inheritdoc />
    public async ValueTask ShowAvailableQuestsAsync()
    {
        var connection = this._player.Connection;
        if (connection is null || this._player.OpenedNpc is null)
        {
            return;
        }

        var totalQuests = this._player.GetAvailableQuestsOfOpenedNpc().ToList();

        // The length field of this C1 packet is a single byte; long quest lists must be chunked.
        var maxQuestsPerPacket = 0;
        while (maxQuestsPerPacket < byte.MaxValue
            && AvailableQuestsRef.GetRequiredSize(maxQuestsPerPacket + 1) <= byte.MaxValue)
        {
            maxQuestsPerPacket++;
        }

        maxQuestsPerPacket = Math.Max(1, maxQuestsPerPacket);

        if (totalQuests.Count == 0)
        {
            await connection.SendAsync(() =>
            {
                var size = AvailableQuestsRef.GetRequiredSize(0);
                var span = connection.Output.GetSpan(size)[..size];
                _ = new AvailableQuestsRef(span)
                {
                    QuestCount = 0,
                    QuestNpcNumber = (ushort)this._player.OpenedNpc.Definition.Number,
                };
                return size;
            }).ConfigureAwait(false);
            return;
        }

        for (var offset = 0; offset < totalQuests.Count; offset += maxQuestsPerPacket)
        {
            var chunk = totalQuests.Skip(offset).Take(maxQuestsPerPacket).ToList();
            int Write()
            {
                var size = AvailableQuestsRef.GetRequiredSize(chunk.Count);
                var span = connection.Output.GetSpan(size)[..size];
                var packet = new AvailableQuestsRef(span)
                {
                    QuestCount = (ushort)chunk.Count,
                    QuestNpcNumber = (ushort)this._player.OpenedNpc.Definition.Number,
                };

                for (int i = 0; i < chunk.Count; i++)
                {
                    var block = packet[i];
                    var quest = chunk[i];
                    block.Number = (ushort)quest.StartingNumber;
                    block.Group = (ushort)quest.Group;
                }

                return size;
            }

            await connection.SendAsync(Write).ConfigureAwait(false);
        }
    }
}