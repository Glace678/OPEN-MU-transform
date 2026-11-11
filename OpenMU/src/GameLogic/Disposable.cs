// <copyright file="Disposable.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic;

/// <summary>
/// Base class for a disposable class.
/// </summary>
public class Disposable : IDisposable
{
    private int _disposeState;

    /// <summary>
    /// Finalizes an instance of the <see cref="Disposable"/> class.
    /// </summary>
    ~Disposable()
    {
        this.Dispose(false);
    }

    /// <summary>
    /// Gets a value indicating whether this instance is disposed.
    /// </summary>
    /// <value>
    ///   <c>true</c> if this instance is disposed; otherwise, <c>false</c>.
    /// </value>
    public bool IsDisposed
    {
        get => System.Threading.Volatile.Read(ref this._disposeState) == 2;
        protected set => System.Threading.Volatile.Write(ref this._disposeState, value ? 2 : 0);
    }

    /// <summary>
    /// Gets a value indicating whether this instance is disposing.
    /// </summary>
    /// <value>
    ///   <c>true</c> if this instance is disposing; otherwise, <c>false</c>.
    /// </value>
    public bool IsDisposing => System.Threading.Volatile.Read(ref this._disposeState) == 1;

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
    /// </summary>
    public void Dispose()
    {
        if (this.TryBeginDispose())
        {
            var succeeded = false;
            try
            {
                this.Dispose(true);
                succeeded = true;
            }
            finally
            {
                this.CompleteDispose(succeeded);
            }
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Atomically transitions this instance from active to disposing.
    /// </summary>
    /// <returns><c>true</c> if this caller acquired disposal ownership; otherwise, <c>false</c>.</returns>
    protected bool TryBeginDispose() => System.Threading.Interlocked.CompareExchange(ref this._disposeState, 1, 0) == 0;

    /// <summary>
    /// Completes a disposal attempt initiated by <see cref="TryBeginDispose"/>.
    /// </summary>
    /// <param name="succeeded"><c>true</c> if disposal succeeded; otherwise, <c>false</c>.</param>
    protected void CompleteDispose(bool succeeded) => System.Threading.Volatile.Write(ref this._disposeState, succeeded ? 2 : 0);

    /// <summary>
    /// Releases unmanaged and - optionally - managed resources.
    /// </summary>
    /// <param name="disposing"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
    protected virtual void Dispose(bool disposing)
    {
    }
}