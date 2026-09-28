// <copyright file="AutomaticBackupFailedException.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.LocalLauncher;

/// <summary>
/// Indicates that a normal stop was aborted because its automatic backup failed.
/// </summary>
public sealed class AutomaticBackupFailedException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AutomaticBackupFailedException"/> class.
    /// </summary>
    /// <param name="innerException">The backup failure.</param>
    public AutomaticBackupFailedException(Exception innerException)
        : base($"停止前的自动备份失败：{innerException.Message}", innerException)
    {
    }
}
