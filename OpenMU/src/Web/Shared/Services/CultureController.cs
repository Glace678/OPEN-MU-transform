// <copyright file="CultureController.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.Shared.Services;

using System.Globalization;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// A controller which sets the culture to the specified value and redirects to the specified uri.
/// </summary>
[Route("[controller]/[action]")]
[Microsoft.AspNetCore.Authorization.AllowAnonymous]
public class CultureController : Controller
{
    /// <summary>
    /// Sets the current UI culture by writing a culture cookie and redirects to the specified URI.
    /// </summary>
    /// <param name="culture">The culture name to set, for example &quot;en-US&quot; or &quot;de-DE&quot;. If <see langword="null"/>, the culture is not changed.</param>
    /// <param name="redirectUri">The URI to which the client should be redirected after the culture has been set.</param>
    /// <returns>
    /// An <see cref="IActionResult"/> that performs a local redirect to the specified <paramref name="redirectUri"/>.
    /// </returns>
    public IActionResult Set(string? culture, string? redirectUri)
    {
        if (culture is not null)
        {
            try
            {
                // Validate the culture name; ignore unknown cultures instead of throwing.
                var cultureInfo = CultureInfo.GetCultureInfo(culture);
                var requestCulture = new RequestCulture(cultureInfo);
                var cookieName = CookieRequestCultureProvider.DefaultCookieName;
                var cookieValue = CookieRequestCultureProvider.MakeCookieValue(requestCulture);

                this.HttpContext.Response.Cookies.Append(cookieName, cookieValue);
            }
            catch (CultureNotFoundException)
            {
                // Unknown culture - keep the currently active one.
            }
        }

        if (string.IsNullOrEmpty(redirectUri) || !this.Url.IsLocalUrl(redirectUri))
        {
            // Never redirect to null or to an external URL; fall back to the home page.
            redirectUri = "/";
        }

        return this.LocalRedirect(redirectUri);
    }
}