// <copyright file="MobileGmStatusResponse.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.API;

/// <summary>Defines the mobile GM status response.</summary>
/// <param name="AccountName">The configured local account name.</param>
/// <param name="ServerReady">Whether a game server is started.</param>
/// <param name="Characters">The online characters of that account.</param>
public sealed record MobileGmStatusResponse(string AccountName, bool ServerReady, IReadOnlyList<MobileGmCharacter> Characters);
