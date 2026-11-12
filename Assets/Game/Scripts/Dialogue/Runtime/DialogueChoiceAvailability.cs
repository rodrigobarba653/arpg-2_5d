using System;
using System.Collections.Generic;

/// <summary>
/// Decides which authored choice rows are shown.
/// Uses the frozen session character id and existing <see cref="EventCondition"/> checks.
/// Does not read <see cref="Party.Active"/> and does not change the layout graph.
/// </summary>
public static class DialogueChoiceAvailability
{
    public static bool IsAvailable(DialogueChoiceOption option, string sessionCharacterId)
    {
        if (option == null)
            return false;
        if (!CharacterAllows(option.requiredCharacterId, sessionCharacterId))
            return false;
        return ConditionsAllow(option.availability);
    }

    /// <summary>
    /// Empty requirement is available to every character.
    /// A concrete id must match the frozen session id, ignoring case.
    /// active_player / player / party / hero resolve to that same frozen id.
    /// </summary>
    public static bool CharacterAllows(string requiredCharacterId, string sessionCharacterId)
    {
        if (string.IsNullOrEmpty(requiredCharacterId))
            return true;
        if (string.IsNullOrEmpty(sessionCharacterId))
            return false;

        string resolved = DialogueActiveCharacter.ResolveSpeakerId(requiredCharacterId, sessionCharacterId);
        return string.Equals(resolved, sessionCharacterId, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Empty list means no WorldState restriction.
    /// Null entries are skipped, matching <see cref="EventRunner"/>.
    /// Every remaining condition must be true.
    /// </summary>
    public static bool ConditionsAllow(IList<EventCondition> conditions)
    {
        if (conditions == null || conditions.Count == 0)
            return true;

        for (int i = 0; i < conditions.Count; i++)
        {
            EventCondition condition = conditions[i];
            if (condition == null)
                continue;
            if (!condition.Evaluate())
                return false;
        }

        return true;
    }
}
