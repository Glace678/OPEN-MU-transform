// <copyright file="LetterSendAction.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlayerActions.Messenger;

using MUnique.OpenMU.GameLogic.Views;
using MUnique.OpenMU.GameLogic.Views.Messenger;
using MUnique.OpenMU.Interfaces;
using MUnique.OpenMU.Persistence;

/// <summary>
/// Action to send a letter to another player.
/// </summary>
public class LetterSendAction
{
    /// <summary>
    /// Sends the letter.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="receiver">The receiver.</param>
    /// <param name="message">The message.</param>
    /// <param name="title">The title.</param>
    /// <param name="rotation">The rotation.</param>
    /// <param name="animation">The animation.</param>
    /// <param name="letterId">The client side letter id.</param>
    public async ValueTask SendLetterAsync(Player player, string receiver, string message, string title, byte rotation, byte animation, uint letterId)
    {
        using var loggerScope = player.Logger.BeginScope(this.GetType());
        var sendPrice = player.GameContext.Configuration.LetterSendPrice;
        if (player.Money < sendPrice)
        {
            await player.ShowLocalizedBlueMessageAsync(nameof(PlayerMessage.NotEnoughMoneyToSendLetter)).ConfigureAwait(false);
            await player.InvokeViewPlugInAsync<ILetterSendResultPlugIn>(p => p.LetterSendResultAsync(LetterSendSuccess.NotEnoughMoney, letterId)).ConfigureAwait(false);
            return;
        }

        if (player.SelectedCharacter is null)
        {
            await player.InvokeViewPlugInAsync<ILetterSendResultPlugIn>(p => p.LetterSendResultAsync(LetterSendSuccess.TryAgain, letterId)).ConfigureAwait(false);
            return;
        }

        LetterHeader? letter;
        try
        {
            // Debit before persisting, so a failure leaves no unpaid letter. The pre-check above
            // only guards the UX; a balance change in between makes TryRemoveMoney fail.
            if (sendPrice > 0 && !player.TryRemoveMoney((int)sendPrice))
            {
                await player.ShowLocalizedBlueMessageAsync(nameof(PlayerMessage.NotEnoughMoneyToSendLetter)).ConfigureAwait(false);
                await player.InvokeViewPlugInAsync<ILetterSendResultPlugIn>(p => p.LetterSendResultAsync(LetterSendSuccess.NotEnoughMoney, letterId)).ConfigureAwait(false);
                return;
            }

            using (var context = player.GameContext.PersistenceContextProvider.CreateNewPlayerContext(player.GameContext.Configuration))
            {
                letter = this.CreateLetter(context, player, receiver, message, title, rotation, animation);
                if (!await context.CanSaveLetterAsync(letter).ConfigureAwait(false))
                {
                    // Refund the fee: the letter was not sent.
                    player.TryAddMoney((int)sendPrice);
                    await player.InvokeViewPlugInAsync<ILetterSendResultPlugIn>(p => p.LetterSendResultAsync(LetterSendSuccess.ReceiverNotExists, letterId)).ConfigureAwait(false);
                    return;
                }

                await context.SaveChangesAsync().ConfigureAwait(false);
            }

            await player.InvokeViewPlugInAsync<ILetterSendResultPlugIn>(p => p.LetterSendResultAsync(LetterSendSuccess.Success, letterId)).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Refund the fee on failure, otherwise the player paid for a letter that was not sent.
            if (sendPrice > 0)
            {
                player.TryAddMoney((int)sendPrice);
            }

            player.Logger.LogError(ex, "Unexpected error when trying to send a letter");
            await player.InvokeViewPlugInAsync<ILetterSendResultPlugIn>(p => p.LetterSendResultAsync(LetterSendSuccess.TryAgain, letterId)).ConfigureAwait(false);
            await player.ShowLocalizedBlueMessageAsync(nameof(PlayerMessage.ErrorDuringSendingLetter)).ConfigureAwait(false);
            return;
        }

        // Try to forward it to the player, if he is online
        if ((player.GameContext as IGameServerContext)?.FriendServer is { } friendServer)
        {
            await friendServer.ForwardLetterAsync(letter).ConfigureAwait(false);
        }
    }

    private LetterHeader CreateLetter(IContext context, Player player, string receiver, string message, string title, byte rotation, byte animation)
    {
        var letterHeader = context.CreateNew<LetterHeader>();
        letterHeader.LetterDate = DateTime.UtcNow;
        letterHeader.SenderName = player.SelectedCharacter!.Name;
        letterHeader.ReceiverName = receiver;
        letterHeader.Subject = title;

        var letterBody = context.CreateNew<LetterBody>();
        letterBody.Header = letterHeader;
        letterBody.Message = message;
        letterBody.SenderAppearance = context.CreateNew<AppearanceData>();
        letterBody.SenderAppearance.CharacterClass = player.AppearanceData.CharacterClass;
        player.AppearanceData.EquippedItems.Select(i => i.MakePersistent(context, player.GameContext.Configuration)).ForEach(letterBody.SenderAppearance.EquippedItems.Add);
        letterBody.Rotation = rotation;
        letterBody.Animation = animation;
        return letterHeader;
    }
}