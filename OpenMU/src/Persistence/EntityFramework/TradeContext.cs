// <copyright file="TradeContext.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.EntityFramework;

using Microsoft.EntityFrameworkCore;
using MUnique.OpenMU.Persistence.EntityFramework.Model;

/// <summary>
/// A context which is used for trades.
/// </summary>
/// <remarks>
/// In an ideal world, it would just need to track two properties: <see cref="DataModel.Entities.ItemStorage"/> and <see cref="DataModel.Entities.Item.ItemSlot"/>.
/// </remarks>
/// <seealso cref="Microsoft.EntityFrameworkCore.DbContext" />
internal class TradeContext : AccountContext
{
    /// <inheritdoc/>
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);
        this.Configure(optionsBuilder);
    }

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ItemStorage is intentionally NOT ignored here anymore: the trade settlement must persist
        // both players' wallet balances (data."ItemStorage"."Money") in the very same SaveChanges
        // transaction as the traded item transfer, so this context has to map ItemStorage. Attaching traded
        // items is unaffected, because Item.RawItemStorage is already ignored at the entity level (see
        // AccountContext); dropping this Ignore only allows an explicit wallet shadow to be persisted.
    }
}