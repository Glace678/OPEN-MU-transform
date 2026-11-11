// <copyright file="AsyncDisposable.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic;

/// <summary>
/// Base class for classes which want to implement <see cref="IAsyncDisposable"/>.
/// </summary>
public class AsyncDisposable : Disposable, IAsyncDisposable
{
    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (this.TryBeginDispose())
        {
            var succeeded = false;
            try
            {
                await this.DisposeAsyncCore().ConfigureAwait(false);
                this.Dispose(true);
                succeeded = true;
                GC.SuppressFinalize(this);
            }
            finally
            {
                this.CompleteDispose(succeeded);
            }
        }
    }

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or
    /// resetting unmanaged resources asynchronously.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "VSTHRD200:Use \"Async\" suffix for async methods", Justification = "This naming is suggested by microsoft itself.")]
    protected virtual ValueTask DisposeAsyncCore()
    {
        return ValueTask.CompletedTask;
    }
}