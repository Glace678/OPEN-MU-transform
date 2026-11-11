// <copyright file="ItemOptionList.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.Shared.Components.ItemEdit;

using System.Collections;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.Persistence;

/// <summary>
/// A list of applied item options.
/// It comes to action when multiple options of an <see cref="ItemOptionType"/>
/// can be applied to an item, e.g. excellent options.
/// </summary>
public class ItemOptionList : IList<IncreasableItemOption>
{
    private readonly ItemOptionType _optionType;
    private readonly Item _item;
    private readonly IContext _persistenceContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="ItemOptionList"/> class.
    /// </summary>
    /// <param name="optionType">Type of the option.</param>
    /// <param name="item">The item.</param>
    /// <param name="persistenceContext">The persistence context.</param>
    public ItemOptionList(ItemOptionType optionType, Item item, IContext persistenceContext)
    {
        this._optionType = optionType;
        this._item = item;
        this._persistenceContext = persistenceContext;
    }

    /// <summary>
    /// Occurs when the list has changed.
    /// </summary>
    public event EventHandler? ListChanged;

    /// <inheritdoc />
    public int Count => this.OptionLinks.Count();

    /// <inheritdoc />
    public bool IsReadOnly => false;

    private IEnumerable<ItemOptionLink> OptionLinks => this._item.ItemOptions
        .Where(link => link.ItemOption?.OptionType == this._optionType);

    /// <inheritdoc />
    public IncreasableItemOption this[int index]
    {
        get
        {
            if (index < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            var link = this.OptionLinks.ElementAtOrDefault(index)
                ?? throw new ArgumentOutOfRangeException(nameof(index));
            return link.ItemOption!;
        }

        set => throw new NotSupportedException();
    }

    /// <inheritdoc />
    public IEnumerator<IncreasableItemOption> GetEnumerator()
    {
        return this.OptionLinks.Select(link => link.ItemOption!).GetEnumerator();
    }

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator()
    {
        return this.GetEnumerator();
    }

    /// <inheritdoc />
    public void Add(IncreasableItemOption option)
    {
        var optionLink = this._persistenceContext.CreateNew<ItemOptionLink>();
        optionLink.ItemOption = option;
        this._item.ItemOptions.Add(optionLink);
        this.ListChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public void Clear()
    {
        var currentOptions = this.ToList();
        foreach (var option in currentOptions)
        {
            this.Remove(option);
        }
    }

    /// <inheritdoc />
    public bool Contains(IncreasableItemOption item)
    {
        return this.OptionLinks.Any(o => o.ItemOption == item);
    }

    /// <inheritdoc />
    public void CopyTo(IncreasableItemOption[] array, int arrayIndex)
    {
        ArgumentNullException.ThrowIfNull(array);
        var items = this.OptionLinks.Select(link => link.ItemOption!).ToList();
        items.CopyTo(array, arrayIndex);
    }

    /// <inheritdoc />
    public bool Remove(IncreasableItemOption item)
    {
        if (this.OptionLinks.FirstOrDefault(o => o.ItemOption == item) is { } optionLink)
        {
            return this.RemoveOptionLink(optionLink);
        }

        return false;
    }

    /// <inheritdoc />
    public int IndexOf(IncreasableItemOption item)
    {
        var index = 0;
        foreach (var link in this.OptionLinks)
        {
            if (link.ItemOption == item)
            {
                return index;
            }

            index++;
        }

        return -1;
    }

    /// <inheritdoc />
    public void Insert(int index, IncreasableItemOption item)
    {
        this.Add(item);
    }

    /// <inheritdoc />
    public void RemoveAt(int index)
    {
        if (index < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        var optionLink = this.OptionLinks.ElementAtOrDefault(index)
            ?? throw new ArgumentOutOfRangeException(nameof(index));
        this.RemoveOptionLink(optionLink);
    }

    private bool RemoveOptionLink(ItemOptionLink optionLink)
    {
        if (!this._item.ItemOptions.Remove(optionLink))
        {
            return false;
        }

        var deleteTask = this._persistenceContext.DeleteAsync(optionLink);
        if (!deleteTask.IsCompletedSuccessfully)
        {
            // Observe any asynchronous fault so it never becomes an unobserved task exception.
            _ = deleteTask.AsTask().ContinueWith(
                static t => _ = t.Exception,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously);
        }

        this.ListChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }
}