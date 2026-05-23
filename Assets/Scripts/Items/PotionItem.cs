using UnityEngine;

[CreateAssetMenu(fileName = "Potion", menuName = "ARPG/Items/Potion")]
public class PotionItem : ItemDefinition
{
    [Header("Potion")]
    [Tooltip("Amount of HP restored when consumed.")]
    public int healAmount = 30;

    public override ItemCategory Category => ItemCategory.Potion;
}
