/// <summary>
/// Who the conversation is tied to for bubble placement / EventRunner Start Dialogue.
/// Scene Transforms are not stored on the asset — only Player vs NPC speaker id.
/// </summary>
public enum DialogueOriginKind
{
    ActivePlayer = 0,
    Npc = 1,
}
