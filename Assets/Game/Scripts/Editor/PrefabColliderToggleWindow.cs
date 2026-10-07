using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Turns every collider under the selected prefab / hierarchy on or off.
/// Works on scene instances and Prefab Mode. Does not touch FBX assets in the Project window.
/// </summary>
public class PrefabColliderToggleWindow : EditorWindow
{
    bool includeTriggers = true;
    Vector2 scroll;

    [MenuItem("Tools/ARPG/Level Design/Prefab Collider Toggle", false, 107)]
    public static void Open()
    {
        var w = GetWindow<PrefabColliderToggleWindow>("Prefab Collider Toggle");
        w.minSize = new Vector2(360, 220);
    }

    void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);

        EditorGUILayout.LabelField("Prefab Collider Toggle", EditorStyles.boldLabel);
        EditorGUILayout.LabelField(
            "Select a prefab instance (or several). Enable or disable all colliders in each hierarchy.",
            EditorStyles.wordWrappedMiniLabel);
        EditorGUILayout.Space(8);

        GameObject[] roots = GetIndependentRoots(Selection.gameObjects);
        if (roots.Length == 0)
        {
            EditorGUILayout.HelpBox("Select one or more GameObjects in the Scene or Prefab Mode.", MessageType.Info);
            EditorGUILayout.EndScrollView();
            return;
        }

        if (roots.Length == 1)
            EditorGUILayout.LabelField("Selected:", roots[0].name);
        else
            EditorGUILayout.LabelField("Selected roots:", roots.Length.ToString());

        int total = 0;
        int enabled = 0;
        int triggers = 0;
        foreach (var root in roots)
        {
            if (root == null)
                continue;
            var cols = root.GetComponentsInChildren<Collider>(true);
            total += cols.Length;
            for (int i = 0; i < cols.Length; i++)
            {
                if (cols[i] == null)
                    continue;
                if (cols[i].enabled)
                    enabled++;
                if (cols[i].isTrigger)
                    triggers++;
            }
        }

        EditorGUILayout.LabelField("Colliders found:", $"{total}  (enabled {enabled}, triggers {triggers})");
        EditorGUILayout.Space(4);

        includeTriggers = EditorGUILayout.ToggleLeft(
            new GUIContent("Include Trigger Colliders", "Off keeps door/ladder/water triggers alone."),
            includeTriggers);

        EditorGUILayout.Space(8);

        using (new EditorGUI.DisabledScope(total == 0))
        {
            if (GUILayout.Button("Turn Colliders OFF", GUILayout.Height(28)))
                SetColliders(roots, false);

            if (GUILayout.Button("Turn Colliders ON", GUILayout.Height(28)))
                SetColliders(roots, true);
        }

        EditorGUILayout.EndScrollView();
    }

    void OnSelectionChange()
    {
        Repaint();
    }

    void SetColliders(GameObject[] roots, bool enabled)
    {
        Undo.SetCurrentGroupName(enabled ? "Enable Prefab Colliders" : "Disable Prefab Colliders");
        int group = Undo.GetCurrentGroup();
        int changed = 0;

        foreach (var root in roots)
        {
            if (root == null)
                continue;

            if (IsImmutablePrefabAsset(root))
            {
                Debug.LogWarning(
                    $"[PrefabColliderToggle] Skip '{root.name}': select a scene instance or open Prefab Mode.",
                    root);
                continue;
            }

            var cols = root.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < cols.Length; i++)
            {
                Collider col = cols[i];
                if (col == null)
                    continue;
                if (col.isTrigger && !includeTriggers)
                    continue;
                if (col.enabled == enabled)
                    continue;

                Undo.RecordObject(col, enabled ? "Enable Collider" : "Disable Collider");
                col.enabled = enabled;
                EditorUtility.SetDirty(col);
                if (PrefabUtility.IsPartOfPrefabInstance(col.gameObject))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(col);
                changed++;
            }

            EditorUtility.SetDirty(root);
            if (PrefabUtility.IsPartOfPrefabInstance(root))
                PrefabUtility.RecordPrefabInstancePropertyModifications(root);
        }

        Undo.CollapseUndoOperations(group);
        Debug.Log($"[PrefabColliderToggle] {(enabled ? "Enabled" : "Disabled")} {changed} collider(s).");
        Repaint();
    }

    static GameObject[] GetIndependentRoots(GameObject[] selected)
    {
        if (selected == null || selected.Length == 0)
            return System.Array.Empty<GameObject>();

        var set = new HashSet<GameObject>(selected);
        var roots = new List<GameObject>(selected.Length);
        foreach (var go in selected)
        {
            if (go == null)
                continue;

            bool parentAlsoSelected = false;
            Transform p = go.transform.parent;
            while (p != null)
            {
                if (set.Contains(p.gameObject))
                {
                    parentAlsoSelected = true;
                    break;
                }
                p = p.parent;
            }

            if (!parentAlsoSelected)
                roots.Add(go);
        }

        return roots.ToArray();
    }

    static bool IsImmutablePrefabAsset(GameObject root)
    {
        if (root == null || !PrefabUtility.IsPartOfPrefabAsset(root))
            return false;

        var stage = PrefabStageUtility.GetCurrentPrefabStage();
        if (stage != null && stage.prefabContentsRoot != null)
        {
            Transform contents = stage.prefabContentsRoot.transform;
            if (root.transform == contents || root.transform.IsChildOf(contents))
                return false;
        }

        return true;
    }
}
