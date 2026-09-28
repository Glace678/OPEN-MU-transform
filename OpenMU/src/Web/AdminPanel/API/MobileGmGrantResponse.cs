// <copyright file="MobileGmGrantResponse.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.API;

/// <summary>Defines a mobile GM grant response.</summary>
/// <param name="Success">Whether the item was persisted.</param>
/// <param name="Message">The user-facing result message.</param>
public sealed record MobileGmGrantResponse(bool Success, string Message);
