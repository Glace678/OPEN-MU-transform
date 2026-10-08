// <copyright file="DaprEndpointSecurity.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Dapr.Common;

using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Controllers;

/// <summary>
/// Secures the API controllers of the daprized services against calls from the public network.
/// </summary>
/// <remarks>
/// All service-to-service invocations go through the local Dapr sidecar (daprd), so they
/// originate from the loopback adapter. Public requests are forwarded by the reverse proxy
/// over the container network and therefore never come from loopback.
/// Additionally, the official Dapr "App API token authentication" is supported: when the
/// <c>APP_API_TOKEN</c> environment variable is configured, Dapr sends the token in the
/// <c>dapr-api-token</c> header of every request. Requests are authorized if they either
/// originate from loopback or provide the correct token.
/// </remarks>
internal static class DaprEndpointSecurity
{
    /// <summary>
    /// The name of the environment variable which configures the app API token (same as in Dapr).
    /// </summary>
    public const string TokenEnvironmentVariable = "APP_API_TOKEN";

    /// <summary>
    /// The name of the HTTP header in which Dapr sends the app API token.
    /// </summary>
    public const string TokenHeaderName = "dapr-api-token";

    /// <summary>
    /// Determines whether the specified context belongs to an API controller action.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <returns><c>true</c> if the selected endpoint is a controller action; otherwise, <c>false</c>.</returns>
    public static bool IsControllerAction(HttpContext context)
    {
        return context.GetEndpoint()?.Metadata.GetMetadata<ControllerActionDescriptor>() is { };
    }

    /// <summary>
    /// Determines whether the selected controller action requires authorization. An action (or its
    /// controller) can opt out by carrying <see cref="AllowAnonymousAttribute"/>, which is only
    /// appropriate for intentionally public endpoints (e.g. the public server info).
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <returns><c>true</c> if the request must be authorized; otherwise, <c>false</c>.</returns>
    public static bool RequiresAuthorization(HttpContext context)
    {
        var endpoint = context.GetEndpoint();
        return endpoint?.Metadata.GetMetadata<IAllowAnonymous>() is null;
    }

    /// <summary>
    /// Determines whether the request is allowed to reach the API controller.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <param name="expectedToken">The expected app API token, or <c>null</c> if no token is configured.</param>
    /// <returns><c>true</c> if the request is authorized; otherwise, <c>false</c>.</returns>
    public static bool IsAuthorized(HttpContext context, string? expectedToken)
    {
        if (IsLoopback(context))
        {
            return true;
        }

        if (!string.IsNullOrEmpty(expectedToken)
            && context.Request.Headers.TryGetValue(TokenHeaderName, out var providedToken)
            && FixedTimeEquals(providedToken.ToString(), expectedToken))
        {
            return true;
        }

        return false;
    }

    private static bool IsLoopback(HttpContext context)
    {
        var remoteIp = context.Connection.RemoteIpAddress;
        return remoteIp is { } && IPAddress.IsLoopback(remoteIp);
    }

    private static bool FixedTimeEquals(string provided, string expected)
    {
        var providedBytes = Encoding.UTF8.GetBytes(provided);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        if (providedBytes.Length != expectedBytes.Length)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(expectedBytes, providedBytes);
    }
}
