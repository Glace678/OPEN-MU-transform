// <copyright file="LocalStackStatus.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.LocalLauncher;

/// <summary>
/// Contains a serializable snapshot of the local stack.
/// </summary>
/// <param name="State">The current lifecycle state.</param>
/// <param name="ChangedAt">The time at which the state changed.</param>
/// <param name="DatabaseProcessId">The PostgreSQL process identifier, if known.</param>
/// <param name="ServerProcessId">The OpenMU process identifier, if known.</param>
/// <param name="ClientProcessId">The game client process identifier, if known.</param>
/// <param name="Message">An optional state detail or error.</param>
public sealed record LocalStackStatus(
    LocalStackState State,
    DateTimeOffset ChangedAt,
    int? DatabaseProcessId = null,
    int? ServerProcessId = null,
    int? ClientProcessId = null,
    string? Message = null);
