// <copyright file="RingBuffer.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.Shared;

using MUnique.OpenMU.GameLogic;

/// <summary>
/// A ring buffer of <typeparamref name="T"/>.
/// </summary>
/// <typeparam name="T">The type of items which should be kept inside this instance.</typeparam>
internal class RingBuffer<T>
{
    private readonly T[] _array;

    private int _currentStartIndex;

    private int _nextWriteIndex;

    private int _count;

    /// <summary>
    /// Initializes a new instance of the <see cref="RingBuffer{T}"/> class.
    /// </summary>
    /// <param name="size">The size (capacity) of the buffer.</param>
    public RingBuffer(int size)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);
        this._array = new T[size];
    }

    /// <summary>
    /// Gets the size (capacity) of the buffer.
    /// </summary>
    public int Size => this._array.Length;

    /// <summary>
    /// Gets the enumerable.
    /// </summary>
    /// <returns>The enumerable.</returns>
    public IEnumerable<T> GetEnumerable()
    {
        var snapshot = new List<T>(this._array.Length);
        lock (this._array)
        {
            for (int i = 0; i < this._count; i++)
            {
                var index = (i + this._currentStartIndex) % this._array.Length;
                if (this._array[index] is { } notNull)
                {
                    snapshot.Add(notNull);
                }
            }
        }

        return snapshot;
    }

    /// <summary>
    /// Adds the specified item.
    /// </summary>
    /// <param name="item">The item.</param>
    public void Add(T item)
    {
        lock (this._array)
        {
            this._array[this._nextWriteIndex] = item;
            this._nextWriteIndex = (this._nextWriteIndex + 1) % this.Size;
            if (this._count < this.Size)
            {
                this._count++;
            }
            else
            {
                this._currentStartIndex = this._nextWriteIndex;
            }
        }
    }

    /// <summary>
    /// Clears this instance.
    /// </summary>
    public void Clear()
    {
        lock (this._array)
        {
            this._array.ClearToDefaults();
            this._currentStartIndex = 0;
            this._nextWriteIndex = 0;
            this._count = 0;
        }
    }
}