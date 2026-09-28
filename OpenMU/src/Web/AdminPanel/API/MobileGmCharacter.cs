// <copyright file="MobileGmCharacter.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.API;

/// <summary>Defines one online character in the mobile GM status response.</summary>
/// <param name="Id">The character identifier.</param>
/// <param name="Name">The character name.</param>
/// <param name="Level">The current level.</param>
/// <param name="Money">The current Zen balance carried by the character.</param>
public sealed record MobileGmCharacter(Guid Id, string Name, int Level, long Money);
