// <copyright file="MobileGmItemsResponse.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.API;

/// <summary>Defines the mobile GM item search response.</summary>
/// <param name="Items">The matching safe item definitions.</param>
public sealed record MobileGmItemsResponse(IReadOnlyList<MobileGmItem> Items);
