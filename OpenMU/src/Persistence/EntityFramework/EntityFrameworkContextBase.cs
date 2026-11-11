// <copyright file="EntityFrameworkContextBase.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.EntityFramework;

using System.Collections;
using System.Reflection;
using System.Threading;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Logging;
using MUnique.OpenMU.DataModel.Composition;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.Persistence.EntityFramework.Model;
using Nito.AsyncEx;
using Nito.Disposables;

/// <summary>
/// Abstract base class for an <see cref="IContext"/> which uses an <see cref="DbContext"/>.
/// </summary>
internal class EntityFrameworkContextBase : IContext
{
    private readonly bool _isOwner;
    private readonly IConfigurationChangeListener? _changeListener;
    private readonly AsyncLock _lock = new();
    private readonly ILogger _logger;
    private bool _isDisposed;
    private int _notificationSuspensions;

    /// <summary>
    /// Initializes a new instance of the <see cref="EntityFrameworkContextBase" /> class.
    /// </summary>
    /// <param name="context">The db context.</param>
    /// <param name="repositoryProvider">The repository provider.</param>
    /// <param name="isOwner">If set to <c>true</c>, this instance owns the <see cref="Context" />. That means it will be disposed when this instance will be disposed.</param>
    /// <param name="changeListener">The change listener.</param>
    /// <param name="logger">The logger.</param>
    protected EntityFrameworkContextBase(DbContext context, IContextAwareRepositoryProvider repositoryProvider, bool isOwner, IConfigurationChangeListener? changeListener, ILogger logger)
    {
        this.Context = context;
        this.RepositoryProvider = repositoryProvider;
        this._isOwner = isOwner;
        this._changeListener = changeListener;
        this._logger = logger;

        // Ensure that the model is created.
        _ = context.Model;
    }

    /// <summary>
    /// Finalizes an instance of the <see cref="EntityFrameworkContextBase"/> class.
    /// </summary>
    ~EntityFrameworkContextBase() => this.Dispose(false);

    /// <inheritdoc />
    public bool HasChanges => this.Context.ChangeTracker.HasChanges();

    /// <summary>
    /// Gets the entity framework context.
    /// </summary>
    internal DbContext Context { get; }

    /// <summary>
    /// Gets a value indicating whether this instance owns the underlying <see cref="Context" />
    /// (i.e. disposing this instance disposes the DbContext). When it does, standalone repository
    /// operations (such as a delete) must be persisted by this instance before it is disposed.
    /// </summary>
    internal bool IsContextOwner => this._isOwner;

    /// <summary>
    /// Gets the repository provider.
    /// </summary>
    protected IContextAwareRepositoryProvider RepositoryProvider { get; }

    /// <summary>
    /// Gets the lock synchronizing access to <see cref="Context"/>.
    /// </summary>
    protected AsyncLock Lock => this._lock;

    /// <inheritdoc/>
    public async ValueTask<bool> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // A player's entities can be mutated by game logic on a flow that is not serialized against
        // this save (for example item destruction on an attacker's thread during combat). Such a
        // concurrent mutation makes change detection throw while it enumerates a tracked collection.
        // The mutation is a single, quick operation, so a bounded retry lands on a stable moment
        // instead of failing the whole save - which would otherwise leave the session unpersisted and
        // roll the player back on relog.
        const int maxAttempts = 3;
        var attempt = 0;
        while (true)
        {
            attempt++;
            try
            {
                return await this.SaveChangesCoreAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (DbUpdateConcurrencyException ex) when (attempt < maxAttempts)
            {
                this._logger.LogWarning(ex, "Optimistic concurrency conflict while saving (attempt {Attempt}/{MaxAttempts}); merging and retrying.", attempt, maxAttempts);

                using (await this._lock.LockAsync(cancellationToken).ConfigureAwait(false))
                {
                    // ReloadAsync overwrites the tracked entity with the database values, which would
                    // silently discard exactly the changes this save was supposed to persist. Snapshot
                    // the caller's pending modifications and deletions first and re-apply them after the
                    // refresh, so the retry still saves the player's change on top of the current row.
                    var pendingChanges = new Dictionary<EntityEntry, Dictionary<string, object?>>();
                    var deletedEntries = new HashSet<EntityEntry>();
                    var trackedEntries = this.Context.ChangeTracker.Entries().ToList();
                    foreach (var entry in trackedEntries)
                    {
                        if (entry.State == EntityState.Deleted)
                        {
                            deletedEntries.Add(entry);
                            continue;
                        }

                        var modifiedProperties = entry.Properties.Where(p => p.IsModified).ToList();
                        if (modifiedProperties.Count > 0)
                        {
                            pendingChanges[entry] = modifiedProperties.ToDictionary(p => p.Metadata.Name, p => p.CurrentValue);
                        }
                    }

                    var conflictingEntities = new HashSet<object>(ex.Entries.Select(entry => entry.Entity));
                    foreach (var entry in trackedEntries)
                    {
                        if (conflictingEntities.Contains(entry.Entity) && entry.State != EntityState.Detached)
                        {
                            await entry.ReloadAsync(cancellationToken).ConfigureAwait(false);
                        }
                    }

                    foreach (var deletedEntry in deletedEntries)
                    {
                        if (deletedEntry.State != EntityState.Detached)
                        {
                            deletedEntry.State = EntityState.Deleted;
                        }
                    }

                    foreach (var (entry, values) in pendingChanges)
                    {
                        if (entry.State == EntityState.Detached)
                        {
                            continue;
                        }

                        foreach (var (propertyName, value) in values)
                        {
                            var property = entry.Property(propertyName);
                            property.CurrentValue = value;
                            property.IsModified = true;
                        }
                    }
                }

                await Task.Delay(attempt * 10, cancellationToken).ConfigureAwait(false);
            }
            catch (InvalidOperationException ex) when (attempt < maxAttempts && IsConcurrentEnumerationConflict(ex))
            {
                // A player's entities can be mutated by game logic on a non-serialized flow (e.g.
                // item destruction on an attacker's combat thread), so change detection can fail
                // while enumerating a tracked collection. Nothing is stale or conflicting here, so
                // the correct handling is a plain retry on a stable moment. We must NOT Reload:
                // that would discard the caller's pending changes and silently lose the save.
                this._logger.LogWarning(ex, "Concurrent collection change while saving (attempt {Attempt}/{MaxAttempts}); retrying without reloading.", attempt, maxAttempts);
                await Task.Delay(attempt * 10, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <inheritdoc />
    public IDisposable SuspendChangeNotifications()
    {
        Interlocked.Increment(ref this._notificationSuspensions);
        return new Disposable(() => Interlocked.Decrement(ref this._notificationSuspensions));
    }

    /// <inheritdoc />
    public bool Detach(object item)
    {
        using var l = this._lock.Lock();
        return this.DetachInternal(item);
    }

    /// <inheritdoc />
    public void Attach(object item)
    {
        using var l = this._lock.Lock();
        this.Context.Attach(item);
    }

    /// <inheritdoc />
    public async ValueTask<bool> AttachMoneyBalanceAsync(Guid itemStorageId, int newMoney, CancellationToken cancellationToken = default)
    {
        using var l = await this._lock.LockAsync(cancellationToken).ConfigureAwait(false);

        // The item storage entity type must be mapped by this context (the trade context maps it).
        // Configuration / friend / guild / in-memory contexts do not support it; the default
        // interface implementation already covers those, guard again so an unsupported context
        // reports false instead of throwing on Attach.
        if (!this.IsSupporting(typeof(ItemStorage)))
        {
            return false;
        }

        var existingEntry = this.Context.ChangeTracker.Entries<ItemStorage>()
            .FirstOrDefault(e => e.Entity.Id == itemStorageId);
        if (existingEntry is not null)
        {
            existingEntry.Entity.Money = newMoney;
            if (existingEntry.State == EntityState.Unchanged)
            {
                existingEntry.Property(x => x.Money).IsModified = true;
            }

            return true;
        }

        // Attach a detached shadow of the wallet row (Id + Money only) and mark only Money modified, so the
        // next SaveChanges issues a single UPDATE data."ItemStorage"."Money" WHERE "Id" = @id, in the
        // very same transaction as the traded item transfer. The live player contexts keep tracking
        // their own instance and will re-write the same value on their next periodic save.
        var shadow = new ItemStorage { Id = itemStorageId, Money = newMoney };
        this.Context.Attach(shadow);
        this.Context.Entry(shadow).Property(x => x.Money).IsModified = true;
        return true;
    }

    /// <inheritdoc />
    public T CreateNew<T>(params object?[] args)
        where T : class
    {
        using var l = this._lock.Lock();
        var instance = typeof(CachingEntityFrameworkContext).Assembly.CreateNew<T>(args);
        this.Context.Add(instance);
        return instance;
    }

    /// <inheritdoc />
    public object CreateNew(Type type, params object?[] args)
    {
        using var l = this._lock.Lock();
        var instance = typeof(CachingEntityFrameworkContext).Assembly.CreateNew(type, args);
        this.Context.Add(instance);
        return instance;
    }

    /// <inheritdoc/>
    public async ValueTask<bool> DeleteAsync<T>(T obj)
        where T : class
    {
        using var l = await this._lock.LockAsync();

        var result = false;
        var entry = this.Context.Entry(obj);
        if (entry.State == EntityState.Detached)
        {
            this.Context.Attach(obj);
            entry = this.Context.Entry(obj);
        }

        switch (entry.State)
        {
            case EntityState.Detached:
                return true;
            case EntityState.Added:
                this.DetachInternal(obj);
                break;
            default:
                this.Context.Remove(obj);
                this.ForEachAggregate(obj, a => this.Context.Remove(a));
                break;
        }

        result = true;

        return result;
    }

    /// <inheritdoc/>
    public async Task<T?> GetByIdAsync<T>(Guid id, CancellationToken cancellationToken)
        where T : class
    {
        using var l = await this._lock.LockAsync(cancellationToken);
        using var context = this.RepositoryProvider.ContextStack.UseContext(this);
        return await this.GetRepository<T>().GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<object?> GetByIdAsync(Guid id, Type type, CancellationToken cancellationToken)
    {
        using var l = await this._lock.LockAsync(cancellationToken).ConfigureAwait(false);
        using var context = this.RepositoryProvider.ContextStack.UseContext(this);
        return await this.GetRepository(type).GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<IEnumerable<T>> GetAsync<T>(CancellationToken cancellationToken)
        where T : class
    {
        using var l = await this._lock.LockAsync(cancellationToken).ConfigureAwait(false);
        using var context = this.RepositoryProvider.ContextStack.UseContext(this);
        return await this.GetRepository<T>().GetAllAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<IEnumerable> GetAsync(Type type, CancellationToken cancellationToken)
    {
        using var l = await this._lock.LockAsync(cancellationToken).ConfigureAwait(false);
        using var context = this.RepositoryProvider.ContextStack.UseContext(this);
        return await this.GetRepository(type).GetAllAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public bool IsSupporting(Type type)
    {
        var currentSearchType = type;
        do
        {
            if (currentSearchType is null)
            {
                break;
            }

            if (this.Context.Model.FindLeastDerivedEntityTypes(currentSearchType).FirstOrDefault() is not null)
            {
                return true;
            }

            if (currentSearchType.Name != currentSearchType.BaseType?.Name)
            {
                break;
            }

            currentSearchType = currentSearchType.BaseType;
        }
        while (currentSearchType != typeof(object));

        return false;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (!this._isDisposed)
        {
            this.Dispose(true);
        }

        this._isDisposed = true;
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Determines whether changes of an entity type are published as configuration changes.
    /// </summary>
    /// <param name="entityType">The entity type.</param>
    /// <returns><see langword="true"/> when the entity belongs to the configuration schema.</returns>
    internal static bool PublishesConfigurationChanges(IReadOnlyEntityType entityType)
        => entityType.GetSchema() == SchemaNames.Configuration;

    /// <summary>
    /// Releases unmanaged and - optionally - managed resources.
    /// </summary>
    /// <param name="dispose"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
    protected virtual void Dispose(bool dispose)
    {
        if (!dispose || !this._isOwner)
        {
            return;
        }

        this.Context.Dispose();
    }

    /// <summary>
    /// Determines whether the exception is the specific "collection modified while enumerating"
    /// race which can happen when change detection enumerates a tracked collection while another
    /// thread mutates the player's entities. Other InvalidOperationExceptions are deterministic
    /// bugs and must not be retried.
    /// </summary>
    /// <param name="exception">The exception thrown by the save.</param>
    /// <returns><c>true</c> if the save can safely be retried without reloading anything.</returns>
    private static bool IsConcurrentEnumerationConflict(Exception exception)
    {
        return exception is InvalidOperationException
            && exception.Message.Contains("Collection was modified", StringComparison.Ordinal);
    }

    private async ValueTask<bool> SaveChangesCoreAsync(CancellationToken cancellationToken)
    {
        using var l = await this._lock.LockAsync();

        // when we have a change publisher attached, we want to get the changed entries before accepting them.
        // Otherwise, we can accept them.
        var acceptChanges = true;

        object? sender = null;
        SavedChangesEventArgs? args = null;
        if (this._changeListener is { })
        {
            this.Context.SavedChanges += OnSavedChanges;
            acceptChanges = false;
        }

        try
        {
            await this.Context.SaveChangesAsync(acceptChanges, cancellationToken).ConfigureAwait(false);

            if (args is not null)
            {
                await this.OnSavedChangesAsync(sender, args).ConfigureAwait(false);
            }
        }
        finally
        {
            this.Context.SavedChanges -= OnSavedChanges;
        }

        return true;

        void OnSavedChanges(object? s, SavedChangesEventArgs e)
        {
            sender = s;
            args = e;
        }
    }

    private bool DetachInternal(object item)
    {
        var entry = this.Context.Entry(item);
        if (entry is null)
        {
            return false;
        }

        var previousState = entry.State;
        entry.State = EntityState.Detached;
        this.ForEachAggregate(item, obj => this.DetachInternal(obj));

        return previousState != EntityState.Added;
    }

    private IRepository<T> GetRepository<T>()
        where T : class
    {
        if (this.RepositoryProvider.GetRepository<T>() is { } repository)
        {
            return repository;
        }

        throw new RepositoryNotFoundException(typeof(T));
    }

    private IRepository GetRepository(Type type)
    {
        if (this.RepositoryProvider.GetRepository(type) is { } repository)
        {
            return repository;
        }

        throw new RepositoryNotFoundException(type);
    }

    private void ForEachAggregate(object obj, Action<object> action)
    {
        var aggregateProperties = obj.GetType()
            .GetProperties(BindingFlags.FlattenHierarchy | BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetCustomAttribute<MemberOfAggregateAttribute>() is { }
                        || p.Name.StartsWith("Joined"));
        foreach (var propertyInfo in aggregateProperties)
        {
            var propertyValue = propertyInfo.GetMethod?.Invoke(obj, []);
            if (propertyValue is IEnumerable enumerable)
            {
                foreach (var value in enumerable)
                {
                    action(value);
                }
            }
            else if (propertyValue is { })
            {
                action(propertyValue);
            }
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD100:Avoid async void methods", Justification = "Catching all Exceptions.")]
    private async ValueTask OnSavedChangesAsync(object? sender, SavedChangesEventArgs e)
    {
        try
        {
            if (this._changeListener is null || this._notificationSuspensions > 0)
            {
                // should never be the case
                return;
            }

            if (e.EntitiesSavedCount == 0)
            {
                // why are we getting this event then anyway?
                return;
            }

            var changedEntries = this.Context.ChangeTracker.Entries()
                .Where(entity => entity.State != EntityState.Unchanged
                                 && PublishesConfigurationChanges(entity.Metadata))
                .ToList();
            foreach (var entry in changedEntries)
            {
                var (parent, parentCollectionNavigation) = this.GetParentInformation(entry);

                switch (entry.State)
                {
                    case EntityState.Added:
                        await this._changeListener.ConfigurationAddedAsync(entry.Metadata.ClrType, entry.Entity.GetId(), entry.Entity, parent, parentCollectionNavigation).ConfigureAwait(false);
                        break;
                    case EntityState.Deleted:
                        await this._changeListener.ConfigurationRemovedAsync(entry.Metadata.ClrType, entry.Entity.GetId(), parent, parentCollectionNavigation).ConfigureAwait(false);
                        break;
                    case EntityState.Modified:
                        await this._changeListener.ConfigurationChangedAsync(entry.Metadata.ClrType, entry.Entity.GetId(), entry.Entity, parent).ConfigureAwait(false);
                        break;
                    default:
                        // no change publishing required.
                        break;
                }

                if (parent is not null && parent is not Model.GameConfiguration && parent is not Guid)
                {
                    await this._changeListener.ConfigurationChangedAsync(parent.GetType(), parent.GetId(), parent, null).ConfigureAwait(false);
                }
            }
        }
        catch (Exception ex)
        {
            this._logger.LogError(ex, "Unexpected error publishing changes.");
        }
        finally
        {
            try
            {
                this.Context.ChangeTracker.AcceptAllChanges();
            }
            catch (Exception ex)
            {
                this._logger.LogError(ex, "Unexpected error when accepting all saved changes.");
            }
        }
    }

    private (object? Parent, INavigationBase? ParentCollectionNavigation) GetParentInformation(EntityEntry entry)
    {
        var propertyToParent = entry.Properties
            .FirstOrDefault(p => p.Metadata.IsForeignKey()
                                 && (p.Metadata.IsShadowProperty() || p.Metadata.PropertyInfo?.GetCustomAttribute<IsLinkToParentAttribute>() is not null));

        var parentId = (Guid?)(propertyToParent?.CurrentValue ?? propertyToParent?.OriginalValue);
        if (parentId is null)
        {
            return (null, null);
        }

        object? parent = null;
        INavigationBase? parentCollectionNavigation = null;
        var parentEntry = this.Context.ChangeTracker.Entries().FirstOrDefault(e => e.Entity.GetId() == parentId);
        if (parentEntry is not null && propertyToParent is not null)
        {
            parent = parentEntry.Entity;
            var parentCollection = parentEntry.Collections
                .FirstOrDefault(c => c.Metadata.IsCollection
                                     && (c.Metadata as INavigation)?.ForeignKey == propertyToParent.Metadata.GetContainingForeignKeys().FirstOrDefault());
            parentCollectionNavigation = parentCollection?.Metadata;
        }

        return (parent ?? parentId, parentCollectionNavigation);
    }
}
