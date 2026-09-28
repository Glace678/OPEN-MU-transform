// <copyright file="LocalStackState.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.LocalLauncher;

/// <summary>
/// Describes the lifecycle state of the local OpenMU stack.
/// </summary>
public enum LocalStackState
{
    /// <summary>The stack is stopped.</summary>
    Stopped,

    /// <summary>The stack is validating and initializing local data.</summary>
    Initializing,

    /// <summary>PostgreSQL is starting.</summary>
    StartingDatabase,

    /// <summary>OpenMU is starting.</summary>
    StartingServer,

    /// <summary>The stack is ready for a game client.</summary>
    Running,

    /// <summary>The stack is stopping gracefully.</summary>
    Stopping,

    /// <summary>The stack failed and requires attention.</summary>
    Faulted,
}
