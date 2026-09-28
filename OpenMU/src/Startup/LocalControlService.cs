// <copyright file="LocalControlService.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Startup;

using System.IO;
using System.IO.Pipes;
using System.Text.Json;
using System.Threading;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>
/// Provides the current Windows user with a private status and graceful shutdown channel.
/// </summary>
internal sealed class LocalControlService : BackgroundService
{
    internal const string PipeEnvironmentVariableName = "OPENMU_CONTROL_PIPE";

    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger<LocalControlService> _logger;
    private readonly string? _pipeName;

    /// <summary>
    /// Initializes a new instance of the <see cref="LocalControlService"/> class.
    /// </summary>
    /// <param name="lifetime">The application lifetime.</param>
    /// <param name="logger">The logger.</param>
    public LocalControlService(IHostApplicationLifetime lifetime, ILogger<LocalControlService> logger)
    {
        this._lifetime = lifetime;
        this._logger = logger;
        this._pipeName = Environment.GetEnvironmentVariable(PipeEnvironmentVariableName);
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var pipeName = this._pipeName;
        if (string.IsNullOrWhiteSpace(pipeName))
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await this.AcceptCommandAsync(pipeName, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                this._logger.LogError(ex, "The local control channel failed. It will be restarted.");
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken).ConfigureAwait(false);
            }
        }
    }

    private async Task AcceptCommandAsync(string pipeName, CancellationToken stoppingToken)
    {
        await using var pipe = new NamedPipeServerStream(
            pipeName,
            PipeDirection.InOut,
            1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        await pipe.WaitForConnectionAsync(stoppingToken).ConfigureAwait(false);

        using var reader = new StreamReader(pipe, Encoding.UTF8, false, 1024, leaveOpen: true);
        await using var writer = new StreamWriter(pipe, new UTF8Encoding(false), 1024, leaveOpen: true) { AutoFlush = true };
        using var commandTimeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        commandTimeout.CancelAfter(TimeSpan.FromSeconds(5));
        var command = (await reader.ReadLineAsync(commandTimeout.Token).ConfigureAwait(false))?.Trim().ToLowerInvariant();
        switch (command)
        {
            case "status":
                await writer.WriteLineAsync(JsonSerializer.Serialize(new { state = "running", processId = Environment.ProcessId })).ConfigureAwait(false);
                break;
            case "shutdown":
                await writer.WriteLineAsync(JsonSerializer.Serialize(new { state = "stopping", processId = Environment.ProcessId })).ConfigureAwait(false);
                this._lifetime.StopApplication();
                break;
            default:
                await writer.WriteLineAsync(JsonSerializer.Serialize(new { state = "error", error = "unknown-command" })).ConfigureAwait(false);
                break;
        }
    }
}
