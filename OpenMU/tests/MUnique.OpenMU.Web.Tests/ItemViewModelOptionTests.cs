// <copyright file="ItemViewModelOptionTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.Tests;

using System.Reflection;
using Moq;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.Web.Shared.Components.ItemEdit;

/// <summary>
/// Tests for <see cref="ItemViewModel.OnDefinitionChanged"/> (issue 80-46): when switching the item
/// definition, options that are still legal under the new definition must be kept, while options
/// that are not in the new possible-option set (and dangling null links) must be removed. The
/// predicate direction was previously reversed, deleting legal options.
/// </summary>
[TestFixture]
public class ItemViewModelOptionTests
{
    [Test]
    public void OnDefinitionChanged_KeepsLegalOptions_RemovesIllegalAndNull()
    {
        // Arrange: two distinct option instances.
        var legalOption = new IncreasableItemOption();
        var illegalOption = new IncreasableItemOption();

        // The new definition allows only the legal option.
        var optionDef = new ItemOptionDefinition();
        SetCollection(optionDef, nameof(optionDef.PossibleOptions), new List<IncreasableItemOption> { legalOption });
        var newDefinition = new ItemDefinition
        {
            MaximumItemLevel = 15,
            MaximumSockets = 0,
            Durability = 20,
        };
        SetCollection(newDefinition, nameof(newDefinition.PossibleItemOptions), new List<ItemOptionDefinition> { optionDef });

        // The item currently carries one legal, one illegal, and one dangling (null) option link.
        var item = new Item();
        SetCollection(item, nameof(item.ItemOptions), new List<ItemOptionLink>
        {
            new ItemOptionLink { ItemOption = legalOption },
            new ItemOptionLink { ItemOption = illegalOption },
            new ItemOptionLink { ItemOption = null },
        });
        // ItemSetGroups is read by the AncientSet getter/setter during OnDefinitionChanged.
        SetCollection(item, "ItemSetGroups", new List<ItemOfItemSet>());

        var context = new Mock<IContext>();
        context.Setup(c => c.DeleteAsync(It.IsAny<ItemOptionLink>())).ReturnsAsync(true);

        var viewModel = new ItemViewModel(item, context.Object);

        // Act: switch the definition (triggers OnDefinitionChanged).
        viewModel.Definition = newDefinition;

        // Assert: the legal option survives; illegal and dangling links are removed.
        Assert.That(item.ItemOptions.Count(link => ReferenceEquals(link.ItemOption, legalOption)), Is.EqualTo(1));
        Assert.That(item.ItemOptions.Any(link => ReferenceEquals(link.ItemOption, illegalOption)), Is.False,
            "an option not in the new definition's possible set must be removed");
        Assert.That(item.ItemOptions.Any(link => link.ItemOption is null), Is.False,
            "a dangling option link (ItemOption == null) must be removed");
        Assert.That(item.ItemOptions, Has.Exactly(1).Items);
    }

    private static void SetCollection(object entity, string property, object value)
    {
        var prop = entity.GetType().GetProperty(property, BindingFlags.Public | BindingFlags.Instance)!;
        prop.SetValue(entity, value);
    }
}