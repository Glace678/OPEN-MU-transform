// <copyright file="UpdateCharacterStatsExtendedPlugIn.cs" company="MUnique">
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
/// The extended implementation of the <see cref="IUpdateCharacterStatsPlugIn"/> which is forwarding everything to the game client with specific data packets.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.UpdateCharacterStatsExtendedPlugIn_Name), Description = nameof(PlugInResources.UpdateCharacterStatsExtendedPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("851C4579-FB3D-454C-A238-217542E8E6B8")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
public class UpdateCharacterStatsExtendedPlugIn : IUpdateCharacterStatsPlugIn
{
    private readonly RemotePlayer _player;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateCharacterStatsExtendedPlugIn"/> class.
    /// </summary>
    /// <param name="player">The player.</param>
    public UpdateCharacterStatsExtendedPlugIn(RemotePlayer player) => this._player = player;

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
            ? (ulong)expTable[Math.Clamp((int)attributes[Stats.Level] + 1, 0, expTable.Length - 1)]
            : 0UL;
        var maxAttackSpeed = this._player.GameContext.Configuration.Attributes.FirstOrDefault(a => a == Stats.AttackSpeed)?.MaximumValue ?? 200;
        await connection.SendCharacterInformationExtendedAsync(
                this._player.Position.X,
                this._player.Position.Y,
                selectedCharacter.CurrentMap!.Number.ToUnsigned(),
                (ulong)selectedCharacter.Experience,
                nextLevelExp,
                (ushort)Math.Max(0, selectedCharacter.LevelUpPoints),
                (ushort)attributes[Stats.BaseStrength],
                (ushort)attributes[Stats.BaseAgility],
                (ushort)attributes[Stats.BaseVitality],
                (ushort)attributes[Stats.BaseEnergy],
                (ushort)attributes[Stats.BaseLeadership],
                (uint)attributes[Stats.CurrentHealth],
                (uint)attributes[Stats.MaximumHealth],
                (uint)attributes[Stats.CurrentMana],
                (uint)attributes[Stats.MaximumMana],
                (uint)attributes[Stats.CurrentShield],
                (uint)attributes[Stats.MaximumShield],
                (uint)attributes[Stats.CurrentAbility],
                (uint)attributes[Stats.MaximumAbility],
                (uint)this._player.Money,
                selectedCharacter.State.Convert(),
                selectedCharacter.CharacterStatus.Convert(),
                (ushort)selectedCharacter.UsedFruitPoints,
                selectedCharacter.GetMaximumFruitPoints(),
                (ushort)selectedCharacter.UsedNegFruitPoints,
                selectedCharacter.GetMaximumFruitPoints(),
                (ushort)attributes[Stats.AttackSpeed],
                (ushort)attributes[Stats.MagicSpeed],
                (ushort)maxAttackSpeed,
                (byte)selectedCharacter.InventoryExtensions,
                (ushort)attributes[Stats.Resets])
            .ConfigureAwait(false);

        if (selectedCharacter.CharacterClass!.IsMasterClass)
        {
            await this._player.InvokeViewPlugInAsync<IUpdateMasterStatsPlugIn>(p => p.SendMasterStatsAsync()).ConfigureAwait(false);
        }
    }
}