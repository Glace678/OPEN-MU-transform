// <copyright file="LimitedObjectPool.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic;

using System.Collections.Concurrent;
using System.Threading;
using Microsoft.Extensions.ObjectPool;

/// <summary>
/// An <see cref="IObjectPool{T}"/> which limits the amounts of created
/// <typeparamref name="T"/> to the maximum retained objects.
/// </summary>
/// <typeparam name="T">The type of objects to pool.</typeparam>
internal sealed class LimitedObjectPool<T> : DefaultObjectPool<T>, IObjectPool<T>
    where T : class
{
    private readonly SemaphoreSlim _semaphore;
    private readonly ConcurrentDictionary<T, byte> _leased = new(ReferenceEqualityComparer.Instance);
    private int _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="LimitedObjectPool{T}"/> class.
    /// </summary>
    /// <param name="policy">The pooling policy to use.</param>
    /// <param name="maximumRetained">The maximum number of objects to create and retain in the pool.</param>
    public LimitedObjectPool(IPooledObjectPolicy<T> policy, int maximumRetained)
        : base(policy, maximumRetained)
    {
        this._semaphore = new SemaphoreSlim(maximumRetained, maximumRetained);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LimitedObjectPool{T}"/> class.
    /// </summary>
    /// <param name="policy">The pooling policy to use.</param>
    public LimitedObjectPool(IPooledObjectPolicy<T> policy)
        : base(policy, MaximumRetainedDefault)
    {
        this._semaphore = new SemaphoreSlim(MaximumRetainedDefault, MaximumRetainedDefault);
    }

    /// <summary>
    /// Gets the default number of maximum number of pooled objects.
    /// </summary>
    public static int MaximumRetainedDefault => Environment.ProcessorCount * 2;

    /// <inheritdoc />
    public async ValueTask<T> GetAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref this._disposed) != 0, this);
        await this._semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var item = base.Get();
            this._leased[item] = 0;
            return item;
        }
        catch
        {
            this._semaphore.Release();
            throw;
        }
    }

    /// <inheritdoc />
    public override T Get()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref this._disposed) != 0, this);
        this._semaphore.Wait();
        try
        {
            var item = base.Get();
            this._leased[item] = 0;
            return item;
        }
        catch
        {
            this._semaphore.Release();
            throw;
        }
    }

    /// <inheritdoc />
    public override void Return(T obj)
    {
        if (obj is null || !this._leased.TryRemove(obj, out _))
        {
            return;
        }

        base.Return(obj);
        if (Volatile.Read(ref this._disposed) == 0)
        {
            try
            {
                this._semaphore.Release();
            }
            catch (ObjectDisposedException)
            {
                // Pool was disposed concurrently.
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Interlocked.Exchange(ref this._disposed, 1) == 0)
        {
            this._leased.Clear();
            this._semaphore.Dispose();
        }
    }
}