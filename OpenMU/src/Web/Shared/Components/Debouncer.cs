// <copyright file="Debouncer.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.Shared.Components;

using System;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Provides a reusable debounce mechanism that delays execution of an async action
/// until a specified quiet period has elapsed since the last invocation.
/// </summary>
/// <remarks>
/// Thread-safe with respect to rapid sequential calls. Only the last invocation
/// within the debounce window will execute.
/// </remarks>
public sealed class Debouncer : IDisposable
{
    private readonly int _delayMs;
    private readonly object _lock = new();
    private CancellationTokenSource? _cts;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="Debouncer"/> class.
    /// </summary>
    /// <param name="delayMs">The debounce delay in milliseconds. Must be positive.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="delayMs"/> is less than or equal to zero.
    /// </exception>
    public Debouncer(int delayMs)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(delayMs, 0);
        this._delayMs = delayMs;
    }

    /// <summary>
    /// Debounces the specified async action. If called again before the delay elapses,
    /// the previous pending invocation is cancelled and the timer restarts.
    /// </summary>
    /// <param name="action">The async action to execute after the debounce delay.</param>
    /// <returns>A task that completes when the debounce cycle finishes (either executed or cancelled).</returns>
    public async Task DebounceAsync(Func<Task> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        CancellationToken token;
        CancellationTokenSource? previous;
        lock (this._lock)
        {
            if (this._disposed)
            {
                return;
            }

            previous = this._cts;
            this._cts = new CancellationTokenSource();
            token = this._cts.Token;
        }

        CancelAndDispose(previous);

        try
        {
            await Task.Delay(this._delayMs, token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            await action().ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected when a newer invocation cancels a pending debounce, or when the debouncer is disposed.
        }
    }

    /// <summary>
    /// Debounces the specified async action, providing a <see cref="CancellationToken"/>
    /// that the action can observe for cooperative cancellation.
    /// </summary>
    /// <param name="action">
    /// The async action to execute after the debounce delay.
    /// Receives a cancellation token linked to the debounce lifecycle.
    /// </param>
    /// <returns>A task that completes when the debounce cycle finishes.</returns>
    /// <exception cref="ObjectDisposedException">Thrown if the debouncer has been disposed.</exception>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="action"/> is null.</exception>
    public async Task DebounceAsync(Func<CancellationToken, Task> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        CancellationToken token;
        CancellationTokenSource? previous;
        lock (this._lock)
        {
            ObjectDisposedException.ThrowIf(this._disposed, this);
            previous = this._cts;
            this._cts = new CancellationTokenSource();
            token = this._cts.Token;
        }

        CancelAndDispose(previous);

        try
        {
            await Task.Delay(this._delayMs, token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            await action(token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected when a newer invocation cancels a pending debounce, or when the debouncer is disposed.
        }
    }

    /// <summary>
    /// Cancels any currently pending debounced action without disposing the debouncer.
    /// </summary>
    public void Cancel()
    {
        CancellationTokenSource? previous;
        lock (this._lock)
        {
            previous = this._cts;
            this._cts = null;
        }

        CancelAndDispose(previous);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        CancellationTokenSource? previous;
        lock (this._lock)
        {
            if (this._disposed)
            {
                return;
            }

            this._disposed = true;
            previous = this._cts;
            this._cts = null;
        }

        CancelAndDispose(previous);
    }

    private static void CancelAndDispose(CancellationTokenSource? source)
    {
        // Cancellation callbacks can call back into the debouncer. Never run them
        // while holding the state lock, and never dispose a lock with pending waiters.
        if (source is null)
        {
            return;
        }

        try
        {
            source.Cancel();
        }
        finally
        {
            source.Dispose();
        }
    }
}
