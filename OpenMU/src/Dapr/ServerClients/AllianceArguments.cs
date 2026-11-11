// <copyright file="AllianceArguments.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.ServerClients;

/// <summary>
/// Arguments for a guild alliance action or notification.
/// </summary>
public record AllianceArguments(uint MasterGuildId, uint MemberGuildId);
