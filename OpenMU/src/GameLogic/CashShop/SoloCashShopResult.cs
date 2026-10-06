// <copyright file="SoloCashShopResult.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.CashShop;

/// <summary>
/// The result codes of the legacy in-game cash shop protocol, shared by the
/// service and its packet handler. The codes are part of the wire contract, so
/// the values must never change; names describe how the solo implementation
/// uses them. Gifts report some codes under different numbers, listed at the end.
/// </summary>
public static class SoloCashShopResult
{
    /// <summary>The transaction succeeded.</summary>
    public const byte Success = 0;

    /// <summary>Generic failure (insufficient credit/funds, or the delete request in an invalid state).</summary>
    public const byte GenericFailure = 1;

    /// <summary>The requested offer does not exist or does not match the request parameters.</summary>
    public const byte OfferNotFound = 4;

    /// <summary>The player is not in a state where shop operations are allowed.</summary>
    public const byte InvalidPlayerState = 6;

    /// <summary>The account storage is full, or its id space is exhausted.</summary>
    public const byte StorageFull = 2;

    /// <summary>The request used an unsupported coin type or a non-zero mileage flag.</summary>
    public const byte WrongCurrency = 9;

    /// <summary>The named gift recipient does not belong to this account.</summary>
    public const byte UnknownRecipient = 10;

    /// <summary>The character's inventory had no free slot while claiming.</summary>
    public const byte InventoryFull = 21;

    /// <summary>The stored item cannot currently be claimed (invalid state or ungrantable definition).</summary>
    public const byte Unavailable = 22;

    /// <summary>An unexpected error occurred while executing the operation.</summary>
    public const byte InternalError = 255;

    // Gift operations (0x04) report several codes under remapped numbers:
    // an empty/unknown recipient reports 3, and the codes below are the
    // remapped values of the equally named regular codes.

    /// <summary>Gift reporting of an empty or <see cref="UnknownRecipient"/>.</summary>
    public const byte GiftRecipientNotFound = 3;

    /// <summary>Gift reporting of <see cref="WrongCurrency"/>.</summary>
    public const byte GiftWrongCurrency = 10;

    /// <summary>Gift reporting of <see cref="OfferNotFound"/>.</summary>
    public const byte GiftOfferNotFound = 6;

    /// <summary>Gift reporting of <see cref="InvalidPlayerState"/>.</summary>
    public const byte GiftInvalidPlayerState = 7;
}
