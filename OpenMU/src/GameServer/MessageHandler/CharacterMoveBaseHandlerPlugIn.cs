// <copyright file="CharacterMoveBaseHandlerPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.MessageHandler;

using System.Diagnostics;
using Microsoft.Extensions.Logging;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Views.World;
using MUnique.OpenMU.Network.Packets.ClientToServer;
using MUnique.OpenMU.Pathfinding;

/// <summary>
/// Abstract packet handler for character movement packets.
/// Validates coordinate progression, terrain walkability, and enforces anti-cheat boundaries.
/// 角色移动协议抽象处理器，负责校验移动坐标合法性、地形可行走性及执行反作弊位移约束。
/// </summary>
/// <remarks>
/// Movement Anti-Cheat and State Transition Contract (F09 / GS-31 / R2) / 移动反作弊与状态跃迁契约:
/// 1. Packet Framing: Rejects packets shorter than <see cref="InstantMoveRequest.Length"/> to prevent buffer over-reads.
///    报文边界守卫：包长低于瞬时移动请求定长时直接丢弃，严防缓冲区越界解包。
/// 2. Walkability Check: Validates that target coordinates lie within map boundaries and correspond to walkable terrain tiles.
///    可行走性验证：确保目标坐标处于地图有效网格内且地形属性允许通行。
/// 3. Step Distance Clamp: Enforces a maximum step delta (<c>maxAllowedStepDistance = 2.0</c>) to block unauthorized teleportation hacks.
///    步长距离约束：严格限制单步位移欧氏距离不超过 2.0 格，彻底封堵客户端瞬间坐标篡改与瞬移作弊。
/// 4. Rubberband Defense: On detecting illegal moves, forces a rubberband notification (<see cref="IObjectMovedPlugIn"/>) resetting the client.
///    拉回惩罚机制：一旦检测到非法坐标跃迁，立即触发橡胶带回拉包，强制将客户端坐标重置为服务端真实权威坐标。
/// </remarks>
internal abstract class CharacterMoveBaseHandlerPlugIn : IPacketHandlerPlugIn
{
    /// <inheritdoc/>
    public bool IsEncryptionExpected => false;

    /// <inheritdoc/>
    public abstract byte Key { get; }

    /// <inheritdoc/>
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        if (packet.Length < InstantMoveRequest.Length)
        {
            return;
        }

        InstantMoveRequest moveRequest = packet;
        var target = new Point(moveRequest.TargetX, moveRequest.TargetY);

        // When the debugger is attached, developers are permitted to freely move/teleport for debugging purposes.
        if (Debugger.IsAttached)
        {
            await player.MoveAsync(target).ConfigureAwait(false);
            return;
        }

        var currentMap = player.CurrentMap;
        if (currentMap is null)
        {
            return;
        }

        // Validate terrain boundaries and walkability
        if (target.X >= currentMap.Terrain.WalkMap.GetLength(0) || target.Y >= currentMap.Terrain.WalkMap.GetLength(1)
            || !currentMap.Terrain.WalkMap[target.X, target.Y])
        {
            player.Logger.LogWarning("Player {0} attempted to move to unwalkable position {1}. Rubberbanding.", player.SelectedCharacter?.Name, target);
            await player.InvokeViewPlugInAsync<IObjectMovedPlugIn>(p => p.ObjectMovedAsync(player, MoveType.Instant)).ConfigureAwait(false);
            return;
        }

        // Validate step distance delta: prevent arbitrary client coordinate teleportation.
        const double maxAllowedStepDistance = 2.0;
        var distance = player.Position.EuclideanDistanceTo(target);
        if (distance > maxAllowedStepDistance)
        {
            player.Logger.LogWarning("Player {0} attempted instant move with delta {1} > {2} to {3}. Rubberbanding.", player.SelectedCharacter?.Name, distance, maxAllowedStepDistance, target);
            await player.InvokeViewPlugInAsync<IObjectMovedPlugIn>(p => p.ObjectMovedAsync(player, MoveType.Instant)).ConfigureAwait(false);
            return;
        }

        await player.MoveAsync(target).ConfigureAwait(false);
    }
}