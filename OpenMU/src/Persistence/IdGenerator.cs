// <copyright file="IdGenerator.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence;

using System.Collections.Concurrent;
using System.Threading;

/// <summary>
/// A generator which generates id which should be unique within an instance of a generator.
/// Ids which are no longer used can be given back to the generator, e.g. when an object joins a game map,
/// it gets an id, later when it leaves the map, it gives the id back, so another player can use it.
/// </summary>
/// <remarks>
/// The namespace for this class is probably not the right one - we're missing something like a "Utility" namespace for things like that.
/// </remarks>
public class IdGenerator
{
    private readonly int _firstValue;
    private readonly int _maxValue;
    private readonly ConcurrentQueue<int> _givenBack = new();
    private readonly object _lock = new();
    private int _currentValue;

    /// <summary>
    /// Initializes a new instance of the <see cref="IdGenerator"/> class.
    /// </summary>
    /// <param name="firstValue">The first value.</param>
    /// <param name="maxValue">The maximum value.</param>
    public IdGenerator(int firstValue, int maxValue)
    {
        this._firstValue = firstValue;
        this._maxValue = maxValue;
        this._currentValue = firstValue - 1; // will be increased by 1 by GenerateId
    }

    /// <summary>
    /// Defines how ids are reused, which were given back with <see cref="IdGenerator.GiveBack"/>.
    /// </summary>
    public enum ReUsePolicy
    {
        /// <summary>
        /// The given back id is reused on the next call of <see cref="IdGenerator.GenerateId"/>.
        /// </summary>
        ReUseOnNextRetrieve,

        /// <summary>
        /// The given back id is reused when all of the available ids were exceeded.
        /// </summary>
        ReUseWhenExceeded,
    }

    /// <summary>
    /// Gets or sets the <see cref="ReUsePolicy"/> which defines how ids are reused, which were given back with <see cref="IdGenerator.GiveBack"/>.
    /// </summary>
    public ReUsePolicy ReUseSetting { get; set; }

    /// <summary>
    /// Gets an identifier which is unique within this generator instance.
    /// </summary>
    /// <returns>An identifier which is unique within this generator instance.</returns>
    /// <exception cref="InvalidOperationException">Maximum object id exceeded.</exception>
    public int GenerateId()
    {
        if (this.ReUseSetting == ReUsePolicy.ReUseOnNextRetrieve
            && this.TryDequeueGivenBack(out int next))
        {
            return next;
        }

        while (true)
        {
            var current = Volatile.Read(ref this._currentValue);
            if (current >= this._maxValue)
            {
                if (this.TryDequeueGivenBack(out next))
                {
                    return next;
                }

                throw new InvalidOperationException("Maximum object id exceeded");
            }

            var candidate = current + 1;
            if (Interlocked.CompareExchange(ref this._currentValue, candidate, current) == current)
            {
                return candidate;
            }
        }
    }

    /// <summary>
    /// Recycles an identifier for subsequent reuse by another game object.
    /// 将废弃的对象标识符回收至生成器池中，供后续新加入的对象复用.
    /// </summary>
    /// <param name="id">The identifier to recycle / 待回收的标识符数值.</param>
    /// <remarks>
    /// Id Recycling and Boundary Contract (PER-13 / PER-17 / R2-PER13) / ID 回收与边界安全契约:
    /// 1. Lower and Upper Bounds Check: Only identifiers within [<c>_firstValue</c>, <c>_maxValue</c>] are accepted.
    ///    Negative or out-of-range IDs injected by rogue or erroneous callers are silently rejected.
    ///    数值范围边界校验：仅接受位于起始值与最大值有效闭区间内的 ID；拒绝负数及越界数值回收.
    /// 2. De-duplication: Guarantees that the same identifier cannot be enqueued multiple times concurrently.
    ///    队列去重保护：加锁校验队列内是否已存在相同 ID，规避重复回收造成的并发发号冲突.
    /// 3. Thread Safety: Synchronizes enqueue access via internal lock object (<c>_lock</c>).
    ///    线程同步：通过专用内部锁对象确保多线程回收操作的原子性与内存可见性.
    /// </remarks>
    public void GiveBack(int id)
    {
        if (id >= this._firstValue && id <= this._maxValue)
        {
            lock (this._lock)
            {
                if (!this._givenBack.Contains(id))
                {
                    this._givenBack.Enqueue(id);
                }
            }
        }
    }

    private bool TryDequeueGivenBack(out int id)
    {
        lock (this._lock)
        {
            return this._givenBack.TryDequeue(out id);
        }
    }
}