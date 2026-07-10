using UnityEngine;

public enum ElementType
{
    None,
    Fire,
    Thunder,
    Water,
    Earth,
    Wind,
    Light,
    Dark
}

/// <summary>
/// Identity-only template for a character. Stats (HP/MP/AP/level) live on
/// PlayerHealth in the world, so they can scale with leveling and equipment.
/// </summary>
[CreateAssetMenu(fileName = "Character", menuName = "ARPG/Party/Character Definition")]
public class CharacterDefinition : ScriptableObject
{
    [Header("Identity")]
    public string id;
    public string displayName = "New Character";
    public string className = "Engineer";
    public Sprite portrait;
    public Sprite elementIcon;
    public ElementType element = ElementType.None;
}
