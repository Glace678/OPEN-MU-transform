// <copyright file="CachedRepositoryTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.Tests;

using System.Collections;
using System.Threading;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.Persistence.EntityFramework;

/// <summary>
/// Tests the load/cache semantics of <see cref="CachedRepository{T}"/>.
/// XC-20 pins the behavior the defects relied on: a single shared full load,
/// snapshot results, retry after a failed load, idempotent cache insertion and
/// cache eviction on delete.
/// </summary>
[TestFixture]
public class CachedRepositoryTests
{
    /// <summary>Concurrent getters trigger exactly one base-repository load and all observe it.</summary>
    [Test]
    public async Task ConcurrentGetsShareOneLoadAsync()
    {
        var baseRepository = new FakeRepository { Entity = new TestEntity() };
        var repository = new CachedRepository<TestEntity>(baseRepository);

        var results = await Task.WhenAll(
            repository.GetAllAsync().AsTask(),
            repository.GetAllAsync().AsTask(),
            repository.GetAllAsync().AsTask()).ConfigureAwait(false);

        Assert.That(baseRepository.LoadCount, Is.EqualTo(1));
        foreach (var values in results)
        {
            Assert.That(values, Has.Length.EqualTo(1));
        }
    }

    /// <summary>After a full load the base is never read again and callers receive a materialized snapshot.</summary>
    [Test]
    public async Task FullCacheReturnsSnapshotWithoutReloadAsync()
    {
        var entity = new TestEntity();
        var baseRepository = new FakeRepository { Entity = entity };
        var repository = new CachedRepository<TestEntity>(baseRepository);

        var first = await repository.GetAllAsync().ConfigureAwait(false);
        Assert.That(first, Is.InstanceOf<TestEntity[]>());

        // The base now holds another entity the cache must not observe.
        baseRepository.Entity = new TestEntity();

        var second = await repository.GetAllAsync().ConfigureAwait(false);
        Assert.That(baseRepository.LoadCount, Is.EqualTo(1));
        Assert.That(second, Has.Length.EqualTo(1));
        Assert.That(second.Single(), Is.SameAs(entity));
    }

    /// <summary>A failed load propagates, clears the loading task, and lets the next attempt hit the base again.</summary>
    [Test]
    public async Task FailedLoadIsRetriedOnNextGetAsync()
    {
        var entity = new TestEntity();
        var baseRepository = new FakeRepository { ExceptionOnLoad = new InvalidOperationException("boom") };
        var repository = new CachedRepository<TestEntity>(baseRepository);

        Assert.ThrowsAsync<InvalidOperationException>(
            () => repository.GetAllAsync().AsTask());
        Assert.That(baseRepository.LoadCount, Is.EqualTo(1));

        baseRepository.ExceptionOnLoad = null;
        baseRepository.Entity = entity;

        var values = await repository.GetAllAsync().ConfigureAwait(false);
        Assert.That(baseRepository.LoadCount, Is.EqualTo(2));
        Assert.That(values.Single(), Is.SameAs(entity));
    }

    /// <summary>
    /// Repeated ids in a load collapse to the first instance: the same instance is cached once
    /// and a different conflicting instance is silently skipped rather than failing the load.
    /// </summary>
    [Test]
    public async Task RepeatedIdsInLoadCollapseToFirstInstanceAsync()
    {
        var entity = new TestEntity();
        var duplicateBase = new FakeRepository { Entities = { entity, entity } };
        var duplicateRepository = new CachedRepository<TestEntity>(duplicateBase);

        var values = await duplicateRepository.GetAllAsync().ConfigureAwait(false);
        Assert.That(values, Has.Length.EqualTo(1));
        Assert.That(values.Single(), Is.SameAs(entity));

        var conflicting = new TestEntity { Id = entity.Id };
        var conflictBase = new FakeRepository { Entities = { entity, conflicting } };
        var conflictRepository = new CachedRepository<TestEntity>(conflictBase);

        var conflictValues = await conflictRepository.GetAllAsync().ConfigureAwait(false);
        Assert.That(conflictValues, Has.Length.EqualTo(1));
        Assert.That(conflictValues.Single(), Is.SameAs(entity));
    }

    /// <summary>AddToCache is idempotent for the same instance and rejects a different instance with the same id.</summary>
    [Test]
    public void AddToCacheIsIdempotentButRejectsConflictingInstance()
    {
        var entity = new TestEntity();
        var repository = new ExposedCachedRepository(new FakeRepository());

        Assert.DoesNotThrow(() => repository.ExposedAdd(entity.Id, entity));
        Assert.DoesNotThrow(() => repository.ExposedAdd(entity.Id, entity));
        Assert.Throws<ArgumentException>(
            () => repository.ExposedAdd(entity.Id, new TestEntity { Id = entity.Id }));
    }

    /// <summary>GetById reads through the loaded cache and reports null for missing ids.</summary>
    [Test]
    public async Task GetByIdReturnsCachedEntityAsync()
    {
        var entity = new TestEntity();
        var baseRepository = new FakeRepository { Entities = { entity } };
        var repository = new CachedRepository<TestEntity>(baseRepository);

        Assert.That(await repository.GetByIdAsync(entity.Id).ConfigureAwait(false), Is.SameAs(entity));
        Assert.That(await repository.GetByIdAsync(Guid.NewGuid()).ConfigureAwait(false), Is.Null);
        Assert.That(baseRepository.LoadCount, Is.EqualTo(1));
    }

    /// <summary>A successful delete evicts the cache entry; a failed base delete leaves it cached.</summary>
    [Test]
    public async Task DeleteEvictsCacheEntryAsync()
    {
        var entity = new TestEntity();
        var baseRepository = new FakeRepository { Entities = { entity } };
        var repository = new CachedRepository<TestEntity>(baseRepository);

        Assert.That(await repository.DeleteAsync(entity.Id).ConfigureAwait(false), Is.True);
        Assert.That(await repository.GetByIdAsync(entity.Id).ConfigureAwait(false), Is.Null);

        var other = new TestEntity();
        var failingBase = new FakeRepository { Entities = { other }, DeleteResult = false };
        var failingRepository = new CachedRepository<TestEntity>(failingBase);

        Assert.That(await failingRepository.DeleteAsync(other.Id).ConfigureAwait(false), Is.False);
        Assert.That(await failingRepository.GetByIdAsync(other.Id).ConfigureAwait(false), Is.SameAs(other));
    }

    private sealed class ExposedCachedRepository : CachedRepository<TestEntity>
    {
        public ExposedCachedRepository(IRepository<TestEntity> baseRepository)
            : base(baseRepository)
        {
        }

        public void ExposedAdd(Guid id, TestEntity entity) => this.AddToCache(id, entity);
    }

    private sealed class TestEntity : IIdentifiable
    {
        public TestEntity()
        {
            this.Id = Guid.NewGuid();
        }

        public Guid Id { get; set; }
    }

    private sealed class FakeRepository : IRepository<TestEntity>
    {
        public FakeRepository()
        {
            this.Entities = new List<TestEntity>();
        }

        public List<TestEntity> Entities { get; }

        public TestEntity? Entity
        {
            set
            {
                this.Entities.Clear();
                if (value is not null)
                {
                    this.Entities.Add(value);
                }
            }
        }

        public int LoadCount { get; private set; }

        public Exception? ExceptionOnLoad { get; set; }

        public bool DeleteResult { get; set; } = true;

        public ValueTask<IEnumerable<TestEntity>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            this.LoadCount++;
            if (this.ExceptionOnLoad is not null)
            {
                throw this.ExceptionOnLoad;
            }

            return new ValueTask<IEnumerable<TestEntity>>(this.Entities.ToArray());
        }

        public ValueTask<TestEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return new ValueTask<TestEntity?>(this.Entities.FirstOrDefault(e => e.Id == id));
        }

        public ValueTask<bool> DeleteAsync(object obj)
        {
            return obj is IIdentifiable identifiable
                ? this.DeleteAsync(identifiable.Id)
                : new ValueTask<bool>(false);
        }

        public ValueTask<bool> DeleteAsync(Guid id)
        {
            // Do not mutate the fake's list on a reported failure: a real base
            // repository only applies the delete when the unit of work saves, so
            // a false result leaves the stored row readable.
            var exists = this.Entities.Any(e => e.Id == id);
            if (exists && this.DeleteResult)
            {
                this.Entities.RemoveAll(e => e.Id == id);
                return new ValueTask<bool>(true);
            }

            return new ValueTask<bool>(false);
        }

        async ValueTask<IEnumerable> IRepository.GetAllAsync(CancellationToken cancellationToken)
        {
            return await this.GetAllAsync(cancellationToken).ConfigureAwait(false);
        }

        ValueTask<object?> IRepository.GetByIdAsync(Guid id, CancellationToken cancellationToken)
        {
            return new ValueTask<object?>(this.Entities.FirstOrDefault(e => e.Id == id));
        }
    }
}
