// <copyright file="ConnectionExtensions.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Network;

/// <summary>
/// Extension methods for <see cref="IConnection"/>.
/// Provides high-performance pipelined packet serialization and thread-safe transmission.
/// 提供面向 <see cref="IConnection"/> 的扩展方法，支持高性能流水线数据包序列化与线程安全传输.
/// </summary>
/// <remarks>
/// Architectural Contract / 架构契约:
/// 1. Concurrency: Acquires <see cref="IConnection.OutputLock"/> asynchronously before invoking packet builders to ensure single-writer exclusivity on the underlying <see cref="System.IO.Pipelines.PipeWriter"/>.
///    并发控制：在调用包构造器前异步获取连接输出锁，确保底层 PipeWriter 上的单写者互斥.
/// 2. Zero-Allocation: Packet builders directly format into the rented pipe memory buffer, returning the exact byte count.
///    零分配：包构造器直接向 PipeWriter 租借的内存段写入字节，返回精确写入长度.
/// 3. Lifecycle Guard: Drops transmission immediately if the connection is no longer connected.
///    生命周期守卫：当连接已断开时直接短路退出，规避悬挂连接上的无效锁竞争.
/// </remarks>
public static class ConnectionExtensions
{
    /// <summary>
    /// Sends a message asynchronously using a deferred packet builder delegate.
    /// 使用延迟包构造委托异步发送协议数据包.
    /// </summary>
    /// <param name="connection">The target network connection / 目标网络连接.</param>
    /// <param name="packetBuilder">
    /// The packet builder delegate which formats data directly into <see cref="IConnection.Output"/> and returns the total packet length in bytes.
    /// 包构造器委托，直接向连接输出流缓冲区写入数据并返回包的实际总字节长度.
    /// </param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation / 表示异步发送操作的任务.</returns>
    public static async ValueTask SendAsync(this IConnection connection, Func<int> packetBuilder)
    {
        if (!connection.Connected)
        {
            return;
        }

        using var l = await connection.OutputLock.LockAsync().ConfigureAwait(false);
        if (!connection.Connected)
        {
            return;
        }

        var length = packetBuilder();
        if (length > 0)
        {
            connection.Output.Advance(length);
            await connection.Output.FlushAsync().ConfigureAwait(false);
        }
    }
}