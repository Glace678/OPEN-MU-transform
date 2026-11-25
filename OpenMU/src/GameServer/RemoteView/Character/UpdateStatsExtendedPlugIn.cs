// <copyright file="UpdateStatsExtendedPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView.Character;

using System.Collections.Frozen;
using System.Runtime.InteropServices;
using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.Views.Character;
using MUnique.OpenMU.Network.Packets.ServerToClient;
using MUnique.OpenMU.Network.PlugIns;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// The extended implementation of the <see cref="IUpdateStatsPlugIn"/> which is forwarding everything to the game client with specific data packets.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.UpdateStatsExtendedPlugIn_Name), Description = nameof(PlugInResources.UpdateStatsExtendedPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("E9A1CCBE-416F-41BA-8E74-74CBEB7042DD")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
public class UpdateStatsExtendedPlugIn : UpdateStatsBasePlugIn
{
    private static readonly FrozenDictionary<AttributeDefinition, Func<RemotePlayer, ValueTask>> AttributeChangeActions = new Dictionary<AttributeDefinition, Func<RemotePlayer, ValueTask>>
    {
        { Stats.MaximumHealth, OnMaximumStatsChangedAsync },
        { Stats.MaximumShield, OnMaximumStatsChangedAsync },
        { Stats.MaximumMana, OnMaximumStatsChangedAsync },
        { Stats.MaximumAbility, OnMaximumStatsChangedAsync },
        { Stats.CurrentHealth, OnCurrentStatsChangedAsync },
        { Stats.CurrentShield, OnCurrentStatsChangedAsync },
        { Stats.CurrentMana, OnCurrentStatsChangedAsync },
        { Stats.CurrentAbility, OnCurrentStatsChangedAsync },
        { Stats.AttackSpeed, OnCurrentStatsChangedAsync },
        { Stats.MagicSpeed, OnCurrentStatsChangedAsync },
    }.ToFrozenDictionary();

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateStatsExtendedPlugIn"/> class.
    /// </summary>
    /// <param name="player">The player.</param>
    public UpdateStatsExtendedPlugIn(RemotePlayer player)
        : base(player, AttributeChangeActions)
    {
    }

    private static async ValueTask OnMaximumStatsChangedAsync(RemotePlayer player)
    {
        await player.Connection.SendMaximumStatsExtendedAsync(
            uint.CreateSaturating(player.Attributes![Stats.MaximumHealth]),
            uint.CreateSaturating(player.Attributes[Stats.MaximumShield]),
            uint.CreateSaturating(player.Attributes[Stats.MaximumMana]),
            uint.CreateSaturating(player.Attributes[Stats.MaximumAbility])).ConfigureAwait(false);
    }

    private static async ValueTask OnCurrentStatsChangedAsync(RemotePlayer player)
    {
        await player.Connection.SendCurrentStatsExtendedAsync(
            uint.CreateSaturating(player.Attributes![Stats.CurrentHealth]),
            uint.CreateSaturating(player.Attributes[Stats.CurrentShield]),
            uint.CreateSaturating(player.Attributes[Stats.CurrentMana]),
            uint.CreateSaturating(player.Attributes[Stats.CurrentAbility]),
            ushort.CreateSaturating(player.Attributes[Stats.AttackSpeed]),
            ushort.CreateSaturating(player.Attributes[Stats.MagicSpeed])).ConfigureAwait(false);
    }
}
