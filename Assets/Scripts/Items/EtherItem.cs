using UnityEngine;

[CreateAssetMenu(fileName = "Ether", menuName = "ARPG/Items/Ether")]
public class EtherItem : ItemDefinition
{
    [Header("Ether")]
    [Tooltip("Amount of mana / energy restored when consumed.")]
    public int restoreAmount = 30;

    public override ItemCategory Category => ItemCategory.Ether;
}
