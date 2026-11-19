using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Resolves the interactive portrait for a line.
/// Ambient callers must not use this — ambient never shows portraits.
/// </summary>
public static class DialoguePortraitResolver
{
    public static Sprite Resolve(
        DialogueLineNode line,
        string authoredSpeakerId,
        string resolvedSpeakerId,
        CharacterPortraitRoster roster)
    {
        if (line == null)
            return null;

        DialoguePortraitMode mode = EffectiveMode(line);
        if (mode == DialoguePortraitMode.Hidden)
            return null;

        if (mode == DialoguePortraitMode.Custom)
        {
            Sprite custom = FindVariantSprite(line.portraitVariants, resolvedSpeakerId);
            if (custom != null)
                return custom;
            if (line.portraitSprite != null)
                return line.portraitSprite;
            // Soft fallback so a half-configured Custom line still shows something.
            return ResolveDefault(authoredSpeakerId, resolvedSpeakerId, line.portraitExpression, roster);
        }

        return ResolveDefault(authoredSpeakerId, resolvedSpeakerId, line.portraitExpression, roster);
    }

    static DialoguePortraitMode EffectiveMode(DialogueLineNode line)
    {
        if (line.portraitMode == DialoguePortraitMode.Hidden)
            return DialoguePortraitMode.Hidden;
        if (line.portraitMode == DialoguePortraitMode.Custom)
            return DialoguePortraitMode.Custom;

        // Compat: older assets wrote portraitSprite while mode stayed UseDefault.
        if (line.portraitSprite != null || HasVariantSprites(line.portraitVariants))
            return DialoguePortraitMode.Custom;

        return DialoguePortraitMode.UseDefault;
    }

    static Sprite ResolveDefault(
        string authoredSpeakerId,
        string resolvedSpeakerId,
        string portraitExpression,
        CharacterPortraitRoster roster)
    {
        if (!DialogueActiveCharacter.IsActivePlayerAlias(authoredSpeakerId))
        {
            Sprite fromBinding = DialogueSpeakerBindingLookup.FindDefaultPortrait(authoredSpeakerId);
            if (fromBinding != null)
                return fromBinding;
        }

        if (roster == null)
            return null;

        string expression = string.IsNullOrEmpty(portraitExpression) ? "Neutral" : portraitExpression;
        string key = string.IsNullOrEmpty(resolvedSpeakerId) ? authoredSpeakerId : resolvedSpeakerId;
        return roster.FindPortrait(key, expression);
    }

    public static Sprite FindVariantSprite(
        IList<DialoguePortraitVariant> variants,
        string characterId)
    {
        if (variants == null || string.IsNullOrEmpty(characterId))
            return null;

        for (int i = 0; i < variants.Count; i++)
        {
            DialoguePortraitVariant variant = variants[i];
            if (variant == null || variant.sprite == null)
                continue;
            if (string.Equals(variant.characterId, characterId, StringComparison.OrdinalIgnoreCase))
                return variant.sprite;
        }

        return null;
    }

    static bool HasVariantSprites(IList<DialoguePortraitVariant> variants)
    {
        if (variants == null)
            return false;
        for (int i = 0; i < variants.Count; i++)
        {
            if (variants[i] != null && variants[i].sprite != null)
                return true;
        }
        return false;
    }
}
