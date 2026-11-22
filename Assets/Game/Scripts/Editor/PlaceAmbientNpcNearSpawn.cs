using UnityEditor;
using UnityEngine;

/// <summary>
/// Deprecated entry — use AmbientNpcPrefabCreator.
/// </summary>
public static class PlaceAmbientNpcNearSpawn
{
    [MenuItem("Tools/ARPG/Level Design/Place Ambient NPC Near Spawn", false, 120)]
    public static void PlaceFromMenu()
    {
        AmbientNpcPrefabCreator.ReplaceTestLabMenu();
    }
}
