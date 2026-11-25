// <copyright file="LetterDeleteAction.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlayerActions.Messenger;

using MUnique.OpenMU.GameLogic.Views.Messenger;
using MUnique.OpenMU.Interfaces;

/// <summary>
/// Action to delete a letter.
/// </summary>
public class LetterDeleteAction
{
    /// <summary>
    /// Deletes the letter.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="letter">The letter.</param>
    public async ValueTask DeleteLetterAsync(Player player, LetterHeader letter)
    {
        using var loggerScope = player.Logger.BeginScope(this.GetType());
        if (player.SelectedCharacter is not { } character)
        {
            player.Logger.LogWarning("no character selected, player {0}", player);
            return;
        }

        var letterIndex = character.Letters.IndexOf(letter);
        if (letterIndex < 0)
        {
            // Already deleted (e.g. a repeated request); nothing to do.
            return;
        }

        // The body is a separate persisted entity which references the header. Deleting only the
        // header from the character's list would leave the body behind (or block the header delete
        // on its foreign key), depending on whether it was loaded.
        if (await player.PersistenceContext.GetLetterBodyByHeaderIdAsync(letter.Id).ConfigureAwait(false) is { } letterBody)
        {
            await player.PersistenceContext.DeleteAsync(letterBody).ConfigureAwait(false);
        }

        character.Letters.RemoveAt(letterIndex);
        await player.InvokeViewPlugInAsync<ILetterDeletedPlugIn>(p => p.LetterDeletedAsync((ushort)letterIndex)).ConfigureAwait(false);
    }
}