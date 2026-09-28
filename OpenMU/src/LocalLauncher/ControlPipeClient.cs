// <copyright file="ControlPipeClient.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.LocalLauncher;

using System.IO.Pipes;

/// <summary>
/// Communicates with the current-user-only OpenMU control channel.
/// </summary>
public sealed class ControlPipeClient
{
    private readonly string _pipeName;

    /// <summary>
    /// Initializes a new instance of the <see cref="ControlPipeClient"/> class.
    /// </summary>
    /// <param name="pipeName">The local pipe name.</param>
    public ControlPipeClient(string pipeName)
    {
        this._pipeName = pipeName;
    }

    /// <summary>
    /// Sends one command and returns the JSON response.
    /// </summary>
    /// <param name="command">The command.</param>
    /// <param name="timeout">The connection timeout.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The JSON response line.</returns>
    public async Task<string> SendAsync(string command, TimeSpan timeout, CancellationToken cancellationToken)
    {
        await using var pipe = new NamedPipeClientStream(".", this._pipeName, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);
        await pipe.ConnectAsync(timeoutCts.Token).ConfigureAwait(false);

        await using var writer = new StreamWriter(pipe, new UTF8Encoding(false), 1024, leaveOpen: true) { AutoFlush = true };
        using var reader = new StreamReader(pipe, Encoding.UTF8, false, 1024, leaveOpen: true);
        await writer.WriteLineAsync(command).ConfigureAwait(false);
        return await reader.ReadLineAsync(timeoutCts.Token).ConfigureAwait(false)
               ?? throw new EndOfStreamException("OpenMU 控制通道已关闭，但未返回结果。");
    }
}
