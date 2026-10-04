// <copyright file="CachedRepository{T}.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.EntityFramework;

using System.Collections;
using System.Threading;

/// <summary>
/// A repository which caches all of its data in memory.
/// </summary>
/// <typeparam name="T">The type of the business object.</typeparam>
public class CachedRepository<T> : IRepository<T>
    where T : class, IIdentifiable
{
    private readonly IDictionary<Guid, T> _cache;

    private readonly SemaphoreSlim _loadLock = new(1, 1);

    private bool _allLoaded;

    private Task? _loadingTask;

    /// <summary>
    /// Initializes a new instance of the <see cref="CachedRepository{T}"/> class.
    /// </summary>
    /// <param name="baseRepository">The base repository.</param>
    public CachedRepository(IRepository<T> baseRepository)
    {
        this.BaseRepository = baseRepository;

        this._cache = new Dictionary<Guid, T>();
    }

    /// <summary>
    /// Gets the underlying base repository.
    /// </summary>
    protected IRepository<T> BaseRepository { get; }

    /// <inheritdoc/>
    async ValueTask<IEnumerable> IRepository.GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await this.GetAllAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<IEnumerable<T>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        Task loadingTask;
        await this._loadLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (this._allLoaded)
            {
                return this._cache.Values;
            }

            loadingTask = this._loadingTask ??= this.LoadAllAsync();
        }
        finally
        {
            this._loadLock.Release();
        }

        await loadingTask.WaitAsync(cancellationToken).ConfigureAwait(false);
        return this._cache.Values;
    }

    private async Task LoadAllAsync()
    {
        try
        {
            IEnumerable<T> values = await this.BaseRepository.GetAllAsync(CancellationToken.None).ConfigureAwait(false);
            await this._loadLock.WaitAsync().ConfigureAwait(false);
            try
            {
                foreach (var obj in values)
                {
                    if (!this._cache.ContainsKey(obj.Id))
                    {
                        this.AddToCache(obj.Id, obj);
                    }
                }

                this._allLoaded = true;
            }
            finally
            {
                this._loadLock.Release();
            }
        }
        catch
        {
            await this._loadLock.WaitAsync().ConfigureAwait(false);
            try
            {
                this._loadingTask = null;
            }
            finally
            {
                this._loadLock.Release();
            }

            throw;
        }
    }

    /// <inheritdoc/>
    public async ValueTask<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await this.GetAllAsync(cancellationToken).ConfigureAwait(false);
        this._cache.TryGetValue(id, out var result);
        return result;
    }

    /// <inheritdoc/>
    async ValueTask<object?> IRepository.GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await this.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<bool> DeleteAsync(object obj)
    {
        if (obj is not IIdentifiable identifiable)
        {
            return false;
        }

        return await this.DeleteAsync(identifiable.Id).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<bool> DeleteAsync(Guid id)
    {
        if (!await this.BaseRepository.DeleteAsync(id).ConfigureAwait(false))
        {
            return false;
        }

        this.RemoveFromCache(id);
        return true;
    }

    /// <summary>
    /// Adds the object to the cache.
    /// </summary>
    /// <param name="id">The identifier.</param>
    /// <param name="obj">The object.</param>
    protected virtual void AddToCache(Guid id, T obj)
    {
        if (this._cache.TryGetValue(id, out var value))
        {
            if (Equals(value, obj))
            {
                throw new ArgumentException("Other object with same id is already in cache.");
            }
        }
        else
        {
            this._cache.Add(id, obj);
        }
    }

    /// <summary>
    /// Removes the object from cache.
    /// </summary>
    /// <param name="id">The identifier.</param>
    protected virtual void RemoveFromCache(Guid id)
    {
        this._cache.Remove(id);
    }
}