using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// One button that builds "PlayTestBootstrap.prefab" — a single prefab
/// containing every system a level needs to run standalone (SaveManager,
/// PartyRoot, AudioManager, MenuManager, GameState, PersistentCamera), copied
/// straight out of the Title Screen scene.
///
/// USAGE FOR DESIGNERS:
///   1. Run this once (Tools ▸ ARPG ▸ Level Design ▸ Create Play Test
///      Bootstrap Prefab) — or whenever those systems change in the Title
///      Screen and the prefab needs updating.
///   2. Drag Assets/Game/Prefabs/PlayTestBootstrap.prefab into whatever
///      level scene you're testing.
///   3. Press Play. The level now works exactly like it would from the real
///      Title Screen — no need to open it and click New Game first.
///   4. Remove the prefab from the scene again before saving/shipping it.
/// </summary>
public static class PlayTestBootstrapPrefabCreator
{
    const string TitleScenePath = "Assets/Game/Scenes/TitleScreen.unity";
    const string OutputFolder = "Assets/Game/Prefabs";
    const string OutputPath = OutputFolder + "/PlayTestBootstrap.prefab";

    static readonly System.Type[] WantedTypes =
    {
        typeof(SaveManager),
        typeof(PartyRoot),
        typeof(AudioManager),
        typeof(MenuManager),
        typeof(GameState),
        typeof(PersistentCamera),
    };

    [MenuItem("Tools/ARPG/Level Design/Create Play Test Bootstrap Prefab", false, 105)]
    public static void Create()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Play Test Bootstrap",
                "Sal de Play Mode antes de crear el prefab.", "OK");
            return;
        }

        if (!File.Exists(TitleScenePath))
        {
            EditorUtility.DisplayDialog("Play Test Bootstrap",
                $"No se encontró la escena Title Screen en:\n{TitleScenePath}", "OK");
            return;
        }

        var root = new GameObject("PlayTestBootstrap");
        Scene titleScene = default;
        bool opened = false;
        var roots = new HashSet<GameObject>();

        try
        {
            titleScene = EditorSceneManager.OpenScene(TitleScenePath, OpenSceneMode.Additive);
            opened = true;

            foreach (var type in WantedTypes)
            {
                foreach (var obj in Object.FindObjectsOfType(type))
                {
                    var comp = obj as Component;
                    if (comp == null || comp.gameObject.scene != titleScene) continue;
                    roots.Add(comp.transform.root.gameObject);
                }
            }

            // Party member characters are usually children of PartyRoot (so
            // they come along for free when it's duplicated below), but the
            // slot's root GameObject isn't required to be nested there — add
            // each one explicitly by its own true root so a character placed
            // as a sibling instead of a child still gets included. The
            // HashSet dedupes automatically if it's already covered.
            foreach (var obj in Object.FindObjectsOfType(typeof(PartyRoot)))
            {
                var partyRoot = obj as PartyRoot;
                if (partyRoot == null || partyRoot.gameObject.scene != titleScene) continue;
                if (partyRoot.members == null) continue;

                foreach (var slot in partyRoot.members)
                {
                    if (slot == null || slot.root == null) continue;
                    roots.Add(slot.root.transform.root.gameObject);
                }
            }

            if (roots.Count == 0)
            {
                EditorUtility.DisplayDialog("Play Test Bootstrap",
                    "No se encontró ninguno de los sistemas esperados (SaveManager, PartyRoot, " +
                    "AudioManager, MenuManager, GameState, PersistentCamera) en TitleScreen.", "OK");
                Object.DestroyImmediate(root);
                return;
            }

            foreach (var r in roots)
            {
                var copy = Object.Instantiate(r, root.transform);
                copy.name = r.name;
            }
        }
        finally
        {
            if (opened) EditorSceneManager.CloseScene(titleScene, true);
        }

        // Without this, PartyRoot leaves both characters inactive (HideAll())
        // and nothing ever tells it who should actually start active — that
        // normally only happens via SaveManager.StartNewGame(), which this
        // standalone prefab deliberately skips (it would reload the scene).
        if (root.GetComponent<PlayTestBootstrapKickstarter>() == null)
            root.AddComponent<PlayTestBootstrapKickstarter>();

        if (!AssetDatabase.IsValidFolder(OutputFolder))
            AssetDatabase.CreateFolder("Assets/Game", "Prefabs");

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, OutputPath);
        Object.DestroyImmediate(root);

        Selection.activeObject = prefab;
        EditorGUIUtility.PingObject(prefab);

        Debug.Log($"[PlayTestBootstrapPrefabCreator] Created/updated '{OutputPath}' with " +
                  $"{roots.Count} top-level object(s) (includes party member characters).");

        EditorUtility.DisplayDialog("Play Test Bootstrap",
            "Listo!\n\n" +
            "Para probar un nivel:\n" +
            "1) Arrastra PlayTestBootstrap.prefab a la escena del nivel.\n" +
            "2) Dale Play.\n" +
            "3) Sácalo de la escena antes de guardarla / hacer build.", "OK");
    }
}
