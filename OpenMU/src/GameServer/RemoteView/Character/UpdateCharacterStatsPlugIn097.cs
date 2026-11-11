// <copyright file="UpdateCharacterStatsPlugIn097.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView.Character;

using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.Views.Character;
using MUnique.OpenMU.Network.Packets.ServerToClient;
using MUnique.OpenMU.Network.PlugIns;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// The default implementation of the <see cref="IUpdateCharacterStatsPlugIn"/> which is forwarding everything to the game client with specific data packets.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.UpdateCharacterStatsPlugIn097_Name), Description = nameof(PlugInResources.UpdateCharacterStatsPlugIn097_Description), ResourceType = typeof(PlugInResources))]
[Guid("8ACD9D6B-6FA7-42C3-8C07-E137655CB92F")]
[MinimumClient(0, 97, ClientLanguage.Invariant)]
public class UpdateCharacterStatsPlugIn097 : IUpdateCharacterStatsPlugIn
{
    private readonly RemotePlayer _player;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateCharacterStatsPlugIn097"/> class.
    /// </summary>
    /// <param name="player">The player.</param>
    public UpdateCharacterStatsPlugIn097(RemotePlayer player) => this._player = player;

    /// <inheritdoc/>
    public async ValueTask UpdateCharacterStatsAsync()
    {
        var connection = this._player.Connection;
        if (connection is null || this._player.Account is null || this._player.Attributes is not { } attributes || this._player.SelectedCharacter is not { } selectedCharacter)
        {
            return;
        }

        var expTable = this._player.GameServerContext.ExperienceTable;
        var nextLevelExp = expTable.Length > 0
            ? (uint)expTable[Math.Clamp((int)attributes[Stats.Level] + 1, 0, expTable.Length - 1)]
            : 0U;
        await connection.SendCharacterInformation097Async(
            this._player.Position.X,
            this._player.Position.Y,
            (byte)selectedCharacter.CurrentMap!.Number,
            this._player.Rotation.ToPacketByte(),
            (uint)selectedCharacter.Experience,
            nextLevelExp,
            (ushort)Math.Max(selectedCharacter.LevelUpPoints, 0),
            (ushort)attributes[Stats.BaseStrength],
            (ushort)attributes[Stats.BaseAgility],
            (ushort)attributes[Stats.BaseVitality],
            (ushort)attributes[Stats.BaseEnergy],
            (ushort)attributes[Stats.CurrentHealth],
            (ushort)attributes[Stats.MaximumHealth],
            (ushort)attributes[Stats.CurrentMana],
            (ushort)attributes[Stats.MaximumMana],
            (ushort)attributes[Stats.CurrentAbility],
            (ushort)attributes[Stats.MaximumAbility],
            (uint)this._player.Money,
            selectedCharacter.State.Convert(),
            selectedCharacter.CharacterStatus.Convert(),
            (ushort)selectedCharacter.UsedFruitPoints,
            selectedCharacter.GetMaximumFruitPoints(),
            (ushort)attributes[Stats.BaseLeadership])
            .ConfigureAwait(false);

        await this._player.InvokeViewPlugInAsync<IApplyKeyConfigurationPlugIn>(p => p.ApplyKeyConfigurationAsync()).ConfigureAwait(false);
    }
}