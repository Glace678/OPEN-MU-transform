// <copyright file="OnlineFriend.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.FriendServer;

using System.Threading;
using Nito.AsyncEx.Synchronous;

/// <summary>
/// Represents an online friend who can observe his other friends, and be subscribed by other friends.
/// </summary>
public sealed class OnlineFriend : IObservable<OnlineFriend>, IObserver<OnlineFriend>, IDisposable
{
    private readonly IFriendNotifier _gameServer;

    /// <summary>
    /// The subscribers which want to know state changes of this instance.
    /// </summary>
    private readonly ISet<IObserver<OnlineFriend>> _subscribers;

    /// <summary>
    /// The synchronize object which is used for locks.
    /// </summary>
    private readonly ReaderWriterLockSlim _readerWriterLock = new();

    /// <summary>
    /// This are all subscriptions, to which this player subscribed.
    /// </summary>
    private readonly List<Unsubscriber> _subscriptions;

    private int _isDisposed;

    private static readonly IDisposable EmptySubscription = new NullDisposable();

    /// <summary>
    /// Initializes a new instance of the <see cref="OnlineFriend" /> class.
    /// </summary>
    /// <param name="gameServer">The game server.</param>
    /// <param name="playerName">Name of the player.</param>
    public OnlineFriend(IFriendNotifier gameServer, string playerName)
    {
        this._gameServer = gameServer;
        this.PlayerName = playerName;
        this._subscribers = new HashSet<IObserver<OnlineFriend>>();
        this._subscriptions = new List<Unsubscriber>();
    }

    /// <summary>
    /// Gets the name of the player.
    /// </summary>
    public string PlayerName { get; }

    /// <summary>
    /// Gets or sets the server identifier.
    /// </summary>
    public int ServerId { get; set; }

    /// <summary>
    /// Gets a value indicating whether this friend is invisible or offline.
    /// </summary>
    public bool IsInvisibleOrOffline => this.ServerId == FriendServer.InvisibleServerId || this.ServerId == FriendServer.OfflineServerId;

    /// <summary>
    /// Gets the subscribers which want to know when the online state of the friend changes.
    /// </summary>
    public ISet<IObserver<OnlineFriend>> Subscribers => this._subscribers;

    /// <summary>
    /// Subscribes the specified observer who want to know when the online state of the friend changes.
    /// </summary>
    /// <param name="observer">The observer.</param>
    /// <returns>The <see cref="IDisposable"/> to unsubscribe.</returns>
    public IDisposable Subscribe(IObserver<OnlineFriend> observer)
    {
        if (Volatile.Read(ref this._isDisposed) != 0)
        {
            // Be consistent with the other members (ChangeServer/Unsubscribe/...):
            // a subscription after disposal is a silent no-op instead of throwing.
            return EmptySubscription;
        }

        var readerWriterLock = this._readerWriterLock;
        readerWriterLock.EnterWriteLock();
        try
        {
            if (Volatile.Read(ref this._isDisposed) != 0)
            {
                return EmptySubscription;
            }

            this._subscribers.Add(observer);
        }
        finally
        {
            readerWriterLock.ExitWriteLock();
        }

        return new Unsubscriber(() => this.Unsubscribe(observer));
    }

    /// <summary>
    /// Remembers the subscription of an observation, to be able to unsubscribe later.
    /// </summary>
    /// <param name="subscription">The subscription.</param>
    public void AddSubscription(IDisposable subscription)
    {
        if (subscription is Unsubscriber item)
        {
            this._subscriptions.Add(item);
        }
    }

    /// <summary>
    /// Will be called, when this player is getting removed from the player list.
    /// </summary>
    public void OnCompleted()
    {
        this._subscriptions.ForEach(subscription => subscription.Dispose());
        this.Dispose();
    }

    /// <inheritdoc/>
    public void OnError(Exception error)
    {
        // Method intentionally left empty.
    }

    /// <summary>
    /// Will be called when a subscribed onlinefriend changes his state.
    /// </summary>
    /// <param name="value">online friend with changed state.</param>
    public void OnNext(OnlineFriend value)
    {
        // Send update to this player
        this._gameServer
            .FriendOnlineStateChangedAsync(this.ServerId, this.PlayerName, value.PlayerName, value.ServerId == FriendServer.InvisibleServerId ? FriendServer.OfflineServerId : value.ServerId)
            .AsTask()
            .WaitWithoutException();
    }

    /// <summary>
    /// This player is changing its server. All subscribers will be informed.
    /// </summary>
    /// <param name="serverId">The new server id of this instance.</param>
    public void ChangeServer(int serverId)
    {
        this.ServerId = serverId;

        if (Volatile.Read(ref this._isDisposed) != 0)
        {
            return;
        }

        // Notify every subscriber
        var readerWriterLock = this._readerWriterLock;
        readerWriterLock.EnterReadLock();
        try
        {
            foreach (var friend in this._subscribers)
            {
                friend.OnNext(this);
            }
        }
        finally
        {
            readerWriterLock.ExitReadLock();
        }
    }

    /// <summary>
    /// Determines whether the specified player is a subscriber of this player.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <returns>True, if the specified player is a subscriber of this player.</returns>
    public bool HasSubscriber(OnlineFriend player)
    {
        if (Volatile.Read(ref this._isDisposed) != 0)
        {
            return false;
        }

        var readerWriterLock = this._readerWriterLock;
        readerWriterLock.EnterReadLock();
        try
        {
            return this._subscribers.Contains(player);
        }
        finally
        {
            readerWriterLock.ExitReadLock();
        }
    }

    /// <summary>
    /// Removes the subscriber (friendship ended) and sends him the new state that this instance is offline now.
    /// </summary>
    /// <param name="friend">The player who should no longer get state updates.</param>
    public void RemoveSubscriber(OnlineFriend friend)
    {
        this.Unsubscribe(friend);
        this._gameServer
            .FriendOnlineStateChangedAsync(friend.ServerId, friend.PlayerName, this.PlayerName, FriendServer.OfflineServerId)
            .AsTask()
            .WaitWithoutException();
    }

    /// <inheritdoc/>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2213:DisposableFieldsShouldBeDisposed", MessageId = "_readerWriterLock", Justification = "The lock is intentionally not disposed: a concurrent subscriber/unsubscriber may be blocked on EnterRead/WriteLock when this player is removed, and disposing it then throws ObjectDisposedException. The lock holds no unmanaged resource and is reclaimed by GC once this object is unreachable.")]
    public void Dispose()
    {
        if (Interlocked.Exchange(ref this._isDisposed, 1) != 0)
        {
            return;
        }

        List<Unsubscriber> subscriptions;
        var readerWriterLock = this._readerWriterLock;
        readerWriterLock.EnterWriteLock();
        try
        {
            subscriptions = this._subscriptions.ToList();
            this._subscriptions.Clear();
            this._subscribers.Clear();
        }
        finally
        {
            readerWriterLock.ExitWriteLock();
        }

        subscriptions.ForEach(subscription => subscription.Dispose());
    }

    private void Unsubscribe(IObserver<OnlineFriend> observer)
    {
        if (Volatile.Read(ref this._isDisposed) != 0)
        {
            return;
        }

        var readerWriterLock = this._readerWriterLock;
        readerWriterLock.EnterWriteLock();
        try
        {
            if (Volatile.Read(ref this._isDisposed) != 0)
            {
                return;
            }

            this._subscribers.Remove(observer);
        }
        finally
        {
            readerWriterLock.ExitWriteLock();
        }
    }

    private sealed class NullDisposable : IDisposable
    {
        public void Dispose()
        {
        }
    }

    private sealed class Unsubscriber : IDisposable
    {
        private readonly Action _unsubscribeAction;

        public Unsubscriber(Action unsubscribeAction)
        {
            this._unsubscribeAction = unsubscribeAction;
        }

        public void Dispose()
        {
            this._unsubscribeAction();
        }
    }
}