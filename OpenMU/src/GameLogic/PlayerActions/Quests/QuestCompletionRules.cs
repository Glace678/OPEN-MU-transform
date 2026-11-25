// <copyright file="QuestCompletionRules.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlayerActions.Quests;

using MUnique.OpenMU.DataModel.Configuration.Quests;
using MUnique.OpenMU.DataModel.Entities;

/// <summary>
/// Shared rules which decide whether a quest has already been finished by a character.
/// </summary>
/// <remarks>
/// The persisted quest state keeps the last finished quest per group. Quest numbers are unique and
/// ascending within a group, and the one-time quest chains are strictly sequential - the game client
/// even derives the finished state of all lower numbers from the last finished one. Therefore a
/// non-repeatable quest with a number at or below the last finished number of the same group has
/// already been finished, even when a later quest of that group replaced it as "last finished".
/// Repeatable quests are not affected and keep their original rules.
/// </remarks>
internal static class QuestCompletionRules
{
    /// <summary>
    /// Determines whether the quest has already been finished by the character owning the state.
    /// </summary>
    /// <param name="questState">The quest state of the group, if any.</param>
    /// <param name="quest">The quest to check.</param>
    /// <returns><see langword="true"/> if the quest is already finished; otherwise <see langword="false"/>.</returns>
    public static bool IsFinished(CharacterQuestState? questState, QuestDefinition quest)
    {
        if (questState?.LastFinishedQuest is not { } lastFinishedQuest)
        {
            return false;
        }

        if (Equals(lastFinishedQuest, quest))
        {
            return true;
        }

        return !quest.Repeatable
               && lastFinishedQuest.Group == quest.Group
               && quest.Number <= lastFinishedQuest.Number;
    }
}
