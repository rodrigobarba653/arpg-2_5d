using System;
using UnityEngine;

/// <summary>
/// Optional portrait sprite for one character id (e.g. jyn, ax) on a line.
/// When set, interactive dialogue uses this sprite instead of the roster lookup.
/// </summary>
[Serializable]
public class DialoguePortraitVariant
{
    [Tooltip("Character id this sprite belongs to (jyn, ax, guard1, …).")]
    public string characterId = "";

    [Tooltip("Portrait shown when this character speaks the line.")]
    public Sprite sprite;
}
