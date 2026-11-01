// <copyright file="CollectionToDictionaryAdapter.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence;

using System.Collections;
using System.Diagnostics.CodeAnalysis;

/// <summary>
/// A adapter which adapts from a mapped collection of join entities to a dictionary interface.
/// </summary>
/// <typeparam name="TKey">The type of the key.</typeparam>
/// <typeparam name="TClass">The type of the class.</typeparam>
/// <typeparam name="TPersistent">The persistent type of <typeparamref name="TClass"/>.</typeparam>
/// <typeparam name="TJoin">The type of the join.</typeparam>
public class CollectionToDictionaryAdapter<TKey, TClass, TPersistent, TJoin> : IDictionary<TKey, TClass>
    where TJoin : IDictionaryEntity<TKey, TPersistent>, new()
    where TPersistent : class, TClass, IIdentifiable
    where TClass : class
{
    private readonly ICollection<TJoin> _rawCollection;

    /// <summary>
    /// Initializes a new instance of the <see cref="CollectionToDictionaryAdapter{TKey, TClass, TEfCore, TJoin}"/> class.
    /// </summary>
    /// <param name="rawCollection">The raw collection.</param>
    public CollectionToDictionaryAdapter(ICollection<TJoin> rawCollection)
    {
        this._rawCollection = rawCollection;
    }

    /// <inheritdoc/>
    public ICollection<TKey> Keys => this._rawCollection.Select(join => join.Key).ToList();

    /// <inheritdoc/>
    public ICollection<TClass> Values => this._rawCollection.Select(join => join.Value).OfType<TClass>().ToList();

    /// <inheritdoc/>
    public int Count => this._rawCollection.Count;

    /// <inheritdoc/>
    public bool IsReadOnly => this._rawCollection.IsReadOnly;

    /// <summary>
    /// Gets or sets the element with the specified key in accordance with standard <see cref="IDictionary{TKey, TValue}"/> semantics.
    /// 依据标准 <see cref="IDictionary{TKey, TValue}"/> 规范获取或设置指定键对应的实体元素.
    /// </summary>
    /// <param name="key">The key of the element to get or set / 目标键值.</param>
    /// <returns>The element associated with the specified key / 关联的实体值.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the key does not exist on read access / 读取未注册键时抛出.</exception>
    /// <exception cref="ArgumentException">Thrown when value does not conform to persistent entity type / 传入值类型不匹配持久化类型时抛出.</exception>
    /// <remarks>
    /// Dictionary Indexer Upsert Contract (PER-12 / PER-16 / R2-PER12) / 字典索引器更新插入契约:
    /// 1. Upsert Semantics: Setting a value for an existing key replaces the existing entry (<c>existing.Value = persistentValue</c>)
    ///    rather than throwing duplicate key exceptions, strictly conforming to the .NET <see cref="IDictionary{TKey, TValue}"/> contract.
    ///    更新插入语义：对已存在的键赋值时，更新现有实体的值与关联 ID，而不是盲目抛出重复键异常.
    /// 2. Raw Collection Synchronization: Modifies the underlying mapped join entity collection directly.
    ///    底层集合同步：直接同步修改底层映射的 Join 实体集合，保证 EF Core 与内存状态一致.
    /// </remarks>
    public TClass this[TKey key]
    {
        get
        {
            if (this.TryGetValue(key, out var value))
            {
                return value;
            }

            throw new KeyNotFoundException($"No value found for key {key}.");
        }

        set
        {
            if (value is not TPersistent persistentValue)
            {
                throw new ArgumentException($"{nameof(value)} is not of type {typeof(TPersistent)}", nameof(value));
            }

            if (this._rawCollection.FirstOrDefault(join => object.Equals(join.Key, key)) is { } existing)
            {
                existing.Value = persistentValue;
                existing.ValueId = persistentValue.Id;
            }
            else
            {
                this._rawCollection.Add(new TJoin { Key = key, Value = persistentValue, ValueId = persistentValue.Id });
            }
        }
    }

    /// <inheritdoc/>
    public IEnumerator<KeyValuePair<TKey, TClass>> GetEnumerator()
    {
        foreach (var item in this._rawCollection)
        {
            yield return new KeyValuePair<TKey, TClass>(item.Key, item.Value);
        }
    }

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator()
    {
        return this.GetEnumerator();
    }

    /// <inheritdoc/>
    public void Add(KeyValuePair<TKey, TClass> item)
    {
        this.Add(item.Key, item.Value);
    }

    /// <inheritdoc/>
    public void Add(TKey key, TClass value)
    {
        if (this.ContainsKey(key))
        {
            throw new ArgumentException($"An element with the key '{key}' already exists.", nameof(key));
        }

        if (value is not TPersistent persistentValue)
        {
            throw new ArgumentException($"{nameof(value)} is not of type {typeof(TPersistent)}", nameof(value));
        }

        this._rawCollection.Add(new TJoin { Key = key, Value = persistentValue, ValueId = persistentValue.Id });
    }

    /// <inheritdoc/>
    public void Clear()
    {
        this._rawCollection.Clear();
    }

    /// <inheritdoc/>
    public bool Contains(KeyValuePair<TKey, TClass> item)
    {
        return this.TryGetValue(item.Key, out var value) && EqualityComparer<TClass>.Default.Equals(value, item.Value);
    }

    /// <inheritdoc/>
    public void CopyTo(KeyValuePair<TKey, TClass>[] array, int arrayIndex)
    {
        var keyValuePairs = this.ToList();
        keyValuePairs.CopyTo(array, arrayIndex);
    }

    /// <inheritdoc/>
    public bool Remove(KeyValuePair<TKey, TClass> item)
    {
        if (this._rawCollection.FirstOrDefault(join => object.Equals(join.Key, item.Key) && EqualityComparer<TClass>.Default.Equals(join.Value, item.Value)) is { } joinItem)
        {
            return this._rawCollection.Remove(joinItem);
        }

        return false;
    }

    /// <inheritdoc/>
    public bool Remove(TKey key)
    {
        if (this._rawCollection.FirstOrDefault(join => object.Equals(join.Key, key)) is { } joinItem)
        {
            return this._rawCollection.Remove(joinItem);
        }

        return false;
    }

    /// <inheritdoc/>
    public bool ContainsKey(TKey key)
    {
        return this._rawCollection.Any(join => object.Equals(join.Key, key));
    }

    /// <inheritdoc/>
    public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TClass value)
    {
        value = this._rawCollection.FirstOrDefault(join => object.Equals(join.Key, key))?.Value;
        return value is { };
    }
}