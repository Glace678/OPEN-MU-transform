// <copyright file="MobileGmGrantRequest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.API;

/// <summary>Defines the request body of a mobile item grant.</summary>
/// <param name="RequestId">The idempotency identifier.</param>
/// <param name="CharacterId">The online character identifier.</param>
/// <param name="Group">The item group.</param>
/// <param name="Number">The item number.</param>
/// <param name="Level">The item level (+0..+15).</param>
/// <param name="Quantity">The number of item instances.</param>
/// <param name="HasSkill">Whether capable items should include their skill.</param>
/// <param name="HasLuck">Whether capable equipment should include the luck option.</param>
/// <param name="AdditionalOptionLevel">The additional option level (0 = none, otherwise 1..4 for +4/+8/+12/+16).</param>
/// <param name="ExcellentMask">A bit mask selecting excellent options; bit (Number-1) selects the option with that Number.</param>
public sealed record MobileGmGrantRequest(
    string? RequestId,
    string? CharacterId,
    int Group,
    int Number,
    int Level,
    int Quantity,
    bool HasSkill,
    bool HasLuck = false,
    int AdditionalOptionLevel = 0,
    int ExcellentMask = 0);
