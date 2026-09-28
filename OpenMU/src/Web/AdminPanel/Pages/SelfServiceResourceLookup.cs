// <copyright file="SelfServiceResourceLookup.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.Pages;

using System.Globalization;
using System.Resources;
using MUnique.OpenMU.Web.AdminPanel.Properties;

/// <summary>
/// Resolves self-service validation messages using the current UI culture.
/// </summary>
internal static class SelfServiceResourceLookup
{
    private static readonly ResourceManager ResourceManager = new(
        "MUnique.OpenMU.Web.AdminPanel.Properties.SelfServiceResources",
        typeof(SelfServiceResources).Assembly);

    /// <summary>
    /// Gets a localized self-service resource value.
    /// </summary>
    /// <param name="resourceKey">The resource key.</param>
    /// <returns>The localized value, or the key when no value exists.</returns>
    public static string Get(string resourceKey) =>
        ResourceManager.GetString(resourceKey, CultureInfo.CurrentUICulture) ?? resourceKey;
}
