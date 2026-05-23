using UnityEngine;

[CreateAssetMenu(fileName = "Key", menuName = "ARPG/Items/Key")]
public class KeyItem : ItemDefinition
{
    [Header("Key")]
    [Tooltip("Must match Door.requiredKeyId. Multiple keys can share an id (master key, etc).")]
    public string keyId;

    [Tooltip("If true, the key is consumed when used on a door.")]
    public bool consumeOnUse = false;

    public override ItemCategory Category => ItemCategory.Key;
}
