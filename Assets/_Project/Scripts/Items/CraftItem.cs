using UnityEngine;

[CreateAssetMenu(fileName = "CraftMaterial", menuName = "ARPG/Items/Craft Material")]
public class CraftItem : ItemDefinition
{
    [Header("Crafting")]
    [Tooltip("Free-form tag for grouping materials (e.g. \"metal\", \"herb\", \"wood\").")]
    public string materialTag;

    [Range(1, 5)]
    public int tier = 1;

    public override ItemCategory Category => ItemCategory.Craft;
}
