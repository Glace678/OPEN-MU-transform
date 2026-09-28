// <copyright file="ProcessRunner.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.LocalLauncher;

using System.Diagnostics;
using System.Text;

/// <summary>
/// Runs child processes without passing arguments through a command shell.
/// </summary>
public sealed class ProcessRunner
{
    /// <summary>
    /// Runs a command and waits for it to exit.
    /// </summary>
    /// <param name="executable">The executable path.</param>
    /// <param name="arguments">The individual arguments.</param>
    /// <param name="workingDirectory">The working directory.</param>
    /// <param name="environment">Optional environment overrides.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The process result.</returns>
    public async Task<ProcessResult> RunAsync(
        string executable,
        IEnumerable<string> arguments,
        string workingDirectory,
        IReadOnlyDictionary<string, string?>? environment,
        CancellationToken cancellationToken)
    {
        var startInfo = CreateStartInfo(executable, arguments, workingDirectory, environment);
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException($"无法启动“{executable}”。");
        var standardOutput = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var standardError = process.StandardError.ReadToEndAsync(cancellationToken);
        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            }

            throw;
        }

        return new ProcessResult(process.ExitCode, await standardOutput.ConfigureAwait(false), await standardError.ConfigureAwait(false));
    }

    /// <summary>
    /// Runs a command which starts a long-running descendant that may inherit its output handles.
    /// </summary>
    /// <param name="executable">The executable path.</param>
    /// <param name="arguments">The individual arguments.</param>
    /// <param name="workingDirectory">The working directory.</param>
    /// <param name="environment">Optional environment overrides.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The parent process result.</returns>
    public async Task<ProcessResult> RunWithDetachedDescendantAsync(
        string executable,
        IEnumerable<string> arguments,
        string workingDirectory,
        IReadOnlyDictionary<string, string?>? environment,
        CancellationToken cancellationToken)
    {
        var startInfo = CreateStartInfo(executable, arguments, workingDirectory, environment);
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException($"无法启动“{executable}”。");
        var standardOutput = new StringBuilder();
        var standardError = new StringBuilder();
        var outputGate = new object();
        process.OutputDataReceived += (_, args) => AppendLine(standardOutput, outputGate, args.Data);
        process.ErrorDataReceived += (_, args) => AppendLine(standardError, outputGate, args.Data);
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        try
        {
            await WaitForParentExitAsync(process, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await WaitForParentExitAsync(process, CancellationToken.None).ConfigureAwait(false);
            }

            throw;
        }
        finally
        {
            CancelOutputRead(process);
        }

        lock (outputGate)
        {
            return new ProcessResult(process.ExitCode, standardOutput.ToString(), standardError.ToString());
        }
    }

    /// <summary>
    /// Starts a long-running child process.
    /// </summary>
    /// <param name="executable">The executable path.</param>
    /// <param name="arguments">The individual arguments.</param>
    /// <param name="workingDirectory">The working directory.</param>
    /// <param name="environment">Optional environment overrides.</param>
    /// <returns>The running process.</returns>
    public Process Start(
        string executable,
        IEnumerable<string> arguments,
        string workingDirectory,
        IReadOnlyDictionary<string, string?>? environment)
    {
        var startInfo = CreateStartInfo(executable, arguments, workingDirectory, environment);
        startInfo.UseShellExecute = false;
        return Process.Start(startInfo) ?? throw new InvalidOperationException($"无法启动“{executable}”。");
    }

    private static ProcessStartInfo CreateStartInfo(
        string executable,
        IEnumerable<string> arguments,
        string workingDirectory,
        IReadOnlyDictionary<string, string?>? environment)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        if (environment is not null)
        {
            foreach (var variable in environment)
            {
                if (variable.Value is null)
                {
                    startInfo.Environment.Remove(variable.Key);
                }
                else
                {
                    startInfo.Environment[variable.Key] = variable.Value;
                }
            }
        }

        return startInfo;
    }

    private static void AppendLine(StringBuilder builder, object outputGate, string? line)
    {
        if (line is null)
        {
            return;
        }

        lock (outputGate)
        {
            _ = builder.AppendLine(line);
        }
    }

    private static void CancelOutputRead(Process process)
    {
        try
        {
            process.CancelOutputRead();
        }
        catch (InvalidOperationException)
        {
            // The pipe already reached EOF before the parent process exited.
        }

        try
        {
            process.CancelErrorRead();
        }
        catch (InvalidOperationException)
        {
            // The pipe already reached EOF before the parent process exited.
        }
    }

    private static async Task WaitForParentExitAsync(Process process, CancellationToken cancellationToken)
    {
        var exited = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void HandleExit(object? sender, EventArgs args) => exited.TrySetResult();

        process.EnableRaisingEvents = true;
        process.Exited += HandleExit;
        try
        {
            if (process.HasExited)
            {
                return;
            }

            await exited.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            process.Exited -= HandleExit;
        }
    }
}
