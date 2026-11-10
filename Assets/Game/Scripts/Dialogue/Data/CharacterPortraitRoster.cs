using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "PortraitRoster", menuName = "ARPG/Dialogue/Portrait Roster")]
public class CharacterPortraitRoster : ScriptableObject
{
    public List<CharacterPortraitSet> characters = new List<CharacterPortraitSet>();

    public CharacterPortraitSet FindCharacter(string characterId)
    {
        if (string.IsNullOrEmpty(characterId) || characters == null)
            return null;

        for (int i = 0; i < characters.Count; i++)
        {
            var c = characters[i];
            if (c != null && c.characterId == characterId)
                return c;
        }

        return null;
    }

    public Sprite FindPortrait(string characterId, string expression)
    {
        var set = FindCharacter(characterId);
        if (set == null)
            return null;
        return set.FindExpression(expression);
    }

    public string FindDisplayName(string characterId)
    {
        var set = FindCharacter(characterId);
        if (set == null)
            return characterId;
        return string.IsNullOrEmpty(set.displayName) ? characterId : set.displayName;
    }
}

[Serializable]
public class CharacterPortraitSet
{
    public string characterId = "";
    public string displayName = "";
    public List<PortraitExpression> expressions = new List<PortraitExpression>();

    public Sprite FindExpression(string expressionId)
    {
        if (expressions == null || expressions.Count == 0)
            return null;

        string want = string.IsNullOrEmpty(expressionId) ? "Neutral" : expressionId;
        for (int i = 0; i < expressions.Count; i++)
        {
            var e = expressions[i];
            if (e != null && e.expressionId == want)
                return e.sprite;
        }

        // Fallback: first available.
        return expressions[0] != null ? expressions[0].sprite : null;
    }
}

[Serializable]
public class PortraitExpression
{
    public string expressionId = "Neutral";
    public Sprite sprite;
}
