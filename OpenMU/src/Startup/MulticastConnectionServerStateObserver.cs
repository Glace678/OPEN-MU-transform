// <copyright file="MulticastConnectionServerStateObserver.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Startup;

using System.Collections.Concurrent;
using System.Net;
using MUnique.OpenMU.Interfaces;

/// <summary>
/// An implementation of a <see cref="IGameServerStateObserver"/> which forwards registrations to multiple state observers,
/// e.g when there are multiple connect servers defined for the same game client.
/// </summary>
/// <seealso cref="MUnique.OpenMU.Interfaces.IGameServerStateObserver" />
internal class MulticastConnectionServerStateObserver : IGameServerStateObserver
{
    private readonly object _syncRoot = new();
    private readonly MemorizingObserver _memorizingObserver = new();
    private readonly List<IGameServerStateObserver> _observers = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="MulticastConnectionServerStateObserver"/> class.
    /// </summary>
    public MulticastConnectionServerStateObserver()
    {
        this._observers.Add(this._memorizingObserver);
    }

    /// <summary>
    /// Adds the observer which wants to get notified about changes.
    /// </summary>
    /// <param name="observer">The observer.</param>
    public void AddObserver(IGameServerStateObserver observer)
    {
        lock (this._syncRoot)
        {
            if (!this._observers.Contains(observer))
            {
                this._observers.Add(observer);
            }
        }
    }

    /// <summary>
    /// Removes a previously added observer.
    /// </summary>
    /// <param name="observer">The observer.</param>
    public void RemoveObserver(IGameServerStateObserver observer)
    {
        if (ReferenceEquals(observer, this._memorizingObserver))
        {
            return;
        }

        lock (this._syncRoot)
        {
            this._observers.Remove(observer);
        }
    }

    /// <inheritdoc />
    public void RegisterGameServer(ServerInfo gameServer, IPEndPoint publicEndPoint)
    {
        IGameServerStateObserver[] snapshot;
        lock (this._syncRoot)
        {
            snapshot = this._observers.ToArray();
        }

        for (int i = 0; i < snapshot.Length; i++)
        {
            snapshot[i].RegisterGameServer(gameServer, publicEndPoint);
        }
    }

    /// <inheritdoc />
    public void UnregisterGameServer(ushort gameServerId)
    {
        IGameServerStateObserver[] snapshot;
        lock (this._syncRoot)
        {
            snapshot = this._observers.ToArray();
        }

        for (int i = 0; i < snapshot.Length; i++)
        {
            snapshot[i].UnregisterGameServer(gameServerId);
        }
    }

    /// <inheritdoc />
    public void CurrentConnectionsChanged(ushort serverId, int currentConnections)
    {
        IGameServerStateObserver[] snapshot;
        lock (this._syncRoot)
        {
            snapshot = this._observers.ToArray();
        }

        for (int i = 0; i < snapshot.Length; i++)
        {
            snapshot[i].CurrentConnectionsChanged(serverId, currentConnections);
        }
    }

    /// <summary>
    /// Pulls the registrations.
    /// </summary>
    /// <param name="observer">The observer.</param>
    public void PullRegistrations(IGameServerStateObserver observer)
    {
        foreach (var (serverInfo, endpoint) in this._memorizingObserver.ServerInfos.Values)
        {
            observer.RegisterGameServer(serverInfo, endpoint);
        }
    }

    private class MemorizingObserver : IGameServerStateObserver
    {
        public ConcurrentDictionary<ushort, (ServerInfo ServerInfo, IPEndPoint EndPoint)> ServerInfos { get; } = new();

        public void RegisterGameServer(ServerInfo gameServer, IPEndPoint publicEndPoint)
        {
            this.ServerInfos[gameServer.Id] = (gameServer, publicEndPoint);
        }

        public void UnregisterGameServer(ushort gameServerId)
        {
            this.ServerInfos.TryRemove(gameServerId, out _);
        }

        public void CurrentConnectionsChanged(ushort serverId, int currentConnections)
        {
            if (this.ServerInfos.TryGetValue(serverId, out var tuple))
            {
                tuple.ServerInfo.CurrentConnections = currentConnections;
            }
        }
    }
}