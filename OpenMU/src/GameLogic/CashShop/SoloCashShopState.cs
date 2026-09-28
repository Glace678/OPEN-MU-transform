// <copyright file="SoloCashShopState.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.CashShop;

using System.Text.Json;

/// <summary>
/// Versioned account ledger. An empty existing account receives the welcome grant once.
/// Corrupt or newer data fails closed instead of resetting the player's purchases.
/// </summary>
public sealed class SoloCashShopState
{
    /// <summary>One-time local welcome credit.</summary>
    public const int WelcomeCredit = 1000;

    /// <summary>Bounded account-wide unclaimed item count.</summary>
    public const int StorageCapacity = 512;

    /// <summary>Maximum wallet value.</summary>
    public const int MaximumCredit = 1000000;

    /// <summary>Gets or sets the data format version.</summary>
    public int Version { get; set; } = 1;

    /// <summary>Gets or sets the earned local WCoin(C).</summary>
    public int Credit { get; set; } = WelcomeCredit;

    /// <summary>Gets or sets the next unused storage identifier.</summary>
    public uint NextId { get; set; } = 1;

    /// <summary>Gets or sets the unclaimed purchases.</summary>
    public List<Entry> Entries { get; set; } = [];

    /// <summary>Reads and validates a persisted ledger.</summary>
    public static SoloCashShopState Read(string data)
    {
        var state = string.IsNullOrEmpty(data) ? new SoloCashShopState()
            : JsonSerializer.Deserialize<SoloCashShopState>(data) ?? throw new InvalidOperationException("Empty solo shop ledger.");
        if (state.Version != 1 || state.Credit is < 0 or > MaximumCredit
            || state.NextId is 0 or > int.MaxValue || state.Entries is null
            || state.Entries.Count > StorageCapacity
            || state.Entries.Any(e => e is null || e.Id == 0 || e.Id >= state.NextId
                || e.Level > 15 || e.ItemCode >= 8192 || !double.IsFinite(e.Durability) || e.Durability is <= 0 or > 255
                || e.Price is < 0 or > MaximumCredit || e.Recipient is null || e.Message is null || e.Sender is null)
            || state.Entries.Select(e => e.Id).Distinct().Count() != state.Entries.Count)
        {
            throw new InvalidOperationException("Invalid or unsupported solo shop ledger.");
        }

        return state;
    }

    /// <summary>Serializes a ledger for the account's atomic save.</summary>
    public string Serialize() => JsonSerializer.Serialize(this);

    /// <summary>A permanent item waiting for delivery, snapshotted at purchase time.</summary>
    public sealed record Entry(
        uint Id, uint OfferId, ushort ItemCode, byte Level, double Durability, int Price,
        string Recipient = "", string Sender = "", string Message = "", bool HasSkill = false);
}
