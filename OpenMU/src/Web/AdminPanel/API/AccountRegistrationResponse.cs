// <copyright file="AccountRegistrationResponse.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.API;

/// <summary>
/// Response of account self-service operations: always HTTP 200, the result is in
/// <see cref="Success"/> so the game client can map <see cref="Code"/> to a localized
/// message without parsing translated text. Older clients may ignore RecoveryCode.
/// </summary>
/// <param name="Success">If set to <c>true</c>, the operation succeeded.</param>
/// <param name="Code">A stable result code for clients.</param>
/// <param name="Message">A localized human readable status message.</param>
/// <param name="RecoveryCode">The newly issued recovery code, shown only in this successful response.</param>
public sealed record AccountRegistrationResponse(bool Success, string Code, string Message, string? RecoveryCode = null);
