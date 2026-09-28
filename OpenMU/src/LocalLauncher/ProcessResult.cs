// <copyright file="ProcessResult.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.LocalLauncher;

/// <summary>
/// Contains the captured result of a child process.
/// </summary>
/// <param name="ExitCode">The process exit code.</param>
/// <param name="StandardOutput">The captured standard output.</param>
/// <param name="StandardError">The captured standard error.</param>
public sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError)
{
    /// <summary>
    /// Throws when the command failed.
    /// </summary>
    /// <param name="operation">A non-sensitive operation description.</param>
    public void EnsureSuccess(string operation)
    {
        if (this.ExitCode != 0)
        {
            throw new InvalidOperationException($"{operation}失败，退出代码为 {this.ExitCode}：{this.StandardError.Trim()}");
        }
    }
}
