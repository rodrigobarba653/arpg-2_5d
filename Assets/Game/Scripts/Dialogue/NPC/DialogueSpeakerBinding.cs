using System;
using UnityEngine;

/// <summary>
/// Scene identity for an NPC that can speak in dialogue.
/// DialogueAsset lines store <see cref="speakerId"/> (stable string). This component
/// maps that id to a live Transform + default portrait in the loaded scene.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("ARPG/Dialogue/Speaker Binding")]
public class DialogueSpeakerBinding : MonoBehaviour
{
    [Tooltip("Stable id stored on DialogueLineNode.speakerId. Auto-filled if empty.")]
    public string speakerId = "";

    [Tooltip("Optional display name shown on the chatbox.")]
    public string displayName = "";

    [Tooltip("Default portrait for Use Default lines. Reused across conversations.")]
    public Sprite defaultPortrait;

    [Tooltip("Optional bubble follow point. Empty = this transform.")]
    public Transform speechAnchor;

    public Transform Anchor => speechAnchor != null ? speechAnchor : transform;

    public string ResolvedDisplayName =>
        !string.IsNullOrEmpty(displayName) ? displayName : gameObject.name;

    void OnValidate()
    {
        EnsureSpeakerId();
        if (string.IsNullOrEmpty(displayName))
            displayName = gameObject.name;
    }

    void Reset()
    {
        EnsureSpeakerId();
        displayName = gameObject.name;
    }

    public void EnsureSpeakerId()
    {
        if (!string.IsNullOrEmpty(speakerId))
            return;
        string baseName = Sanitize(gameObject.name);
        if (string.IsNullOrEmpty(baseName))
            baseName = "npc";
        speakerId = baseName + "_" + Guid.NewGuid().ToString("N").Substring(0, 8);
    }

    static string Sanitize(string raw)
    {
        if (string.IsNullOrEmpty(raw))
            return "";
        char[] chars = raw.Trim().ToLowerInvariant().ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            char c = chars[i];
            if (!(c >= 'a' && c <= 'z') && !(c >= '0' && c <= '9'))
                chars[i] = '_';
        }
        return new string(chars).Trim('_');
    }
}
