// <copyright file="MobileGmItem.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.API;

/// <summary>Defines one mobile GM item search result.</summary>
/// <param name="Group">The item group.</param>
/// <param name="Number">The item number.</param>
/// <param name="Name">The localized item name.</param>
/// <param name="MaxLevel">The maximum supported item level.</param>
/// <param name="HasSkill">Whether the definition can carry a skill.</param>
/// <param name="CanHaveLuck">Whether the definition can carry the luck option.</param>
/// <param name="CanHaveAdditionalOption">Whether the definition can carry the additional +defense/+attack option.</param>
/// <param name="ExcellentOptionCount">The number of selectable excellent options.</param>
public sealed record MobileGmItem(
    byte Group,
    short Number,
    string Name,
    byte MaxLevel,
    bool HasSkill,
    bool CanHaveLuck,
    bool CanHaveAdditionalOption,
    int ExcellentOptionCount);
