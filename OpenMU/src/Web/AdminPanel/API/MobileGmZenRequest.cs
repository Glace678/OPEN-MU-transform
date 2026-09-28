// <copyright file="MobileGmZenRequest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.API;

/// <summary>Defines the request body of a mobile money (Zen) grant.</summary>
/// <param name="RequestId">The idempotency identifier.</param>
/// <param name="CharacterId">The online character identifier.</param>
/// <param name="Amount">The amount of Zen to add to the character's inventory.</param>
public sealed record MobileGmZenRequest(
    string? RequestId,
    string? CharacterId,
    long Amount);
