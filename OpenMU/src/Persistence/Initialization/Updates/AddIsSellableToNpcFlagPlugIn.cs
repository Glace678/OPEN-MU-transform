// <copyright file="AddIsSellableToNpcFlagPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.Items;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// This update sets the new <see cref="ItemDefinition.IsSellableToNpc"/> flag to <c>false</c> on quest items,
/// which must never be sold to an NPC merchant. The flag itself defaults to <c>true</c>, so every other
/// item keeps its previous behavior.
/// </summary>
[PlugIn]
[Display(Name = PlugInName, Description = PlugInDescription)]
[Guid("5A2D0E77-9E7C-4B2A-9C4E-1A6F3B8D2C55")]
public class AddIsSellableToNpcFlagPlugIn : UpdatePlugInBase
{
    /// <summary>
    /// The plug in name.
    /// </summary>
    internal const string PlugInName = "Add IsSellableToNpc flag";

    /// <summary>
    /// The plug in description.
    /// </summary>
    internal const string PlugInDescription = "This update marks quest items as not sellable to NPCs, so they can no longer be traded away for zen.";

    /// <inheritdoc />
    public override string Name => PlugInName;

    /// <inheritdoc />
    public override string Description => PlugInDescription;

    /// <inheritdoc />
    public override UpdateVersion Version => UpdateVersion.AddIsSellableToNpcFlag;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 10, 1, 9, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override async ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        var hashSet = new HashSet<short>
        {
            Quest.BrokenSwordNumber,
            Quest.EyeOfAbyssalNumber,
            Quest.FeatherOfDarkPhoenixNumber,
            Quest.FlameOfDeathBeamKnightNumber,
            Quest.HornOfHellMaineNumber,
            Quest.ScrollOfEmperorNumber,
            Quest.SoulShardOfWizardNumber,
            Quest.TearOfElfNumber,
        };
        var questItems = gameConfiguration.Items.Where(item => item.Group == 14 && hashSet.Contains(item.Number));
        foreach (var item in questItems)
        {
            item.IsSellableToNpc = false;
        }
    }
}
