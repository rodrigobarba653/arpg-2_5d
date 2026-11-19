using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Simple Play Animation UI: character, state dropdown, Hold vs Play Once.
/// </summary>
[CustomPropertyDrawer(typeof(PlayCharacterAnimationAction))]
public sealed class PlayCharacterAnimationActionDrawer : PropertyDrawer
{
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        if (property == null)
            return EditorGUIUtility.singleLineHeight;
        if (!property.isExpanded)
            return EditorGUIUtility.singleLineHeight;

        float line = EditorGUIUtility.singleLineHeight + 2f;
        float h = line;
        SerializedProperty character = property.FindPropertyRelative("character");
        if (character != null)
            h += EditorGUI.GetPropertyHeight(character, true) + 2f;
        h += line; // state popup
        h += line; // state manual
        h += line; // mode
        h += line; // help
        var mode = property.FindPropertyRelative("mode");
        if (mode != null && mode.enumValueIndex == (int)PlayCharacterAnimationAction.Mode.PlayOnce)
            h += line; // timeout
        return h + 8f;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        property.isExpanded = EditorGUI.Foldout(
            new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight),
            property.isExpanded,
            label,
            true);

        if (!property.isExpanded)
        {
            EditorGUI.EndProperty();
            return;
        }

        // Migrate old keepAnimation / wait flags into Mode once.
        MigrateLegacy(property);

        EditorGUI.indentLevel++;
        float y = position.y + EditorGUIUtility.singleLineHeight + 2f;
        float line = EditorGUIUtility.singleLineHeight;
        float full = position.width;

        SerializedProperty character = property.FindPropertyRelative("character");
        SerializedProperty stateName = property.FindPropertyRelative("stateName");
        SerializedProperty mode = property.FindPropertyRelative("mode");
        SerializedProperty playOnceTimeout = property.FindPropertyRelative("playOnceTimeoutSeconds");

        if (character != null)
        {
            float ch = EditorGUI.GetPropertyHeight(character, true);
            EditorGUI.PropertyField(new Rect(position.x, y, full, ch), character, true);
            y += ch + 2f;
        }

        var entries = CollectStateEntries(character);
        DrawStatePicker(new Rect(position.x, y, full, line), stateName, entries);
        y += line + 2f;

        EditorGUI.PropertyField(
            new Rect(position.x, y, full, line),
            stateName,
            new GUIContent("State Name (manual)"));
        y += line + 2f;

        EditorGUI.PropertyField(
            new Rect(position.x, y, full, line),
            mode,
            new GUIContent(
                "Mode",
                "Hold Until Event Ends = keep this pose for the rest of the event.\n" +
                "Play Once = play the clip, then continue."));
        y += line + 2f;

        if (mode != null && mode.enumValueIndex == (int)PlayCharacterAnimationAction.Mode.PlayOnce)
        {
            EditorGUI.PropertyField(
                new Rect(position.x, y, full, line),
                playOnceTimeout,
                new GUIContent("Play Once Timeout"));
            y += line + 2f;
        }

        string tip = mode != null && mode.enumValueIndex == (int)PlayCharacterAnimationAction.Mode.HoldUntilEventEnds
            ? "Hold: forces the pose every frame until the EventRunner finishes."
            : "Play Once: waits for the clip (or timeout), then returns locomotion.";
        EditorGUI.HelpBox(new Rect(position.x, y, full, line + 4f), tip, MessageType.Info);

        EditorGUI.indentLevel--;
        EditorGUI.EndProperty();
    }

    static void MigrateLegacy(SerializedProperty property)
    {
        var mode = property.FindPropertyRelative("mode");
        var keep = property.FindPropertyRelative("keepAnimation");
        if (mode == null || keep == null)
            return;
        // Only nudge when still at default Hold and old keep was explicitly used —
        // keepAnimation true already matches Hold default.
        if (keep.boolValue)
            mode.enumValueIndex = (int)PlayCharacterAnimationAction.Mode.HoldUntilEventEnds;
    }

    static void DrawStatePicker(Rect rect, SerializedProperty stateName, List<StateEntry> entries)
    {
        string current = stateName.stringValue ?? "";
        string shown = string.IsNullOrEmpty(current) ? "(pick state…)" : current;
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i].stateName == current)
            {
                shown = entries[i].Label;
                break;
            }
        }

        Rect labelR = new Rect(rect.x, rect.y, EditorGUIUtility.labelWidth, rect.height);
        Rect fieldR = new Rect(
            rect.x + EditorGUIUtility.labelWidth,
            rect.y,
            rect.width - EditorGUIUtility.labelWidth,
            rect.height);
        EditorGUI.LabelField(labelR, "State");

        if (!GUI.Button(fieldR, shown, EditorStyles.popup))
            return;

        var menu = new GenericMenu();
        menu.AddItem(new GUIContent("Clear"), string.IsNullOrEmpty(current), () =>
        {
            stateName.stringValue = "";
            stateName.serializedObject.ApplyModifiedProperties();
        });

        if (entries.Count == 0)
        {
            menu.AddDisabledItem(new GUIContent("No states found"));
        }
        else
        {
            for (int i = 0; i < entries.Count; i++)
            {
                StateEntry e = entries[i];
                string path = e.characterLabel + "/" + e.stateName;
                string captured = e.stateName;
                menu.AddItem(new GUIContent(path), e.stateName == current, () =>
                {
                    stateName.stringValue = captured;
                    stateName.serializedObject.ApplyModifiedProperties();
                });
            }
        }

        menu.DropDown(fieldR);
    }

    struct StateEntry
    {
        public string characterLabel;
        public string stateName;
        public string Label => characterLabel + " / " + stateName;
    }

    static List<StateEntry> CollectStateEntries(SerializedProperty characterProp)
    {
        var list = new List<StateEntry>(64);
        var seen = new HashSet<string>();

        if (characterProp != null)
        {
            var source = characterProp.FindPropertyRelative("source");
            var explicitT = characterProp.FindPropertyRelative("explicitTransform");
            if (source != null &&
                source.enumValueIndex == (int)ScriptedCharacterSource.Explicit &&
                explicitT != null &&
                explicitT.objectReferenceValue is Transform t)
            {
                AddFromTransform(list, seen, t, LabelForTransform(t));
            }
        }

        var partyRoots = Object.FindObjectsByType<PartyRoot>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int r = 0; r < partyRoots.Length; r++)
        {
            var root = partyRoots[r];
            if (root == null || root.members == null)
                continue;
            for (int i = 0; i < root.members.Length; i++)
            {
                var m = root.members[i];
                if (m == null || m.root == null)
                    continue;
                string label = m.character != null && !string.IsNullOrEmpty(m.character.displayName)
                    ? m.character.displayName
                    : m.root.name;
                AddFromTransform(list, seen, m.root.transform, label);
            }
        }

        if (list.Count == 0)
            AddFromProjectControllers(list, seen);

        list.Sort((a, b) =>
        {
            int c = string.CompareOrdinal(a.characterLabel, b.characterLabel);
            return c != 0 ? c : string.CompareOrdinal(a.stateName, b.stateName);
        });
        return list;
    }

    static string LabelForTransform(Transform t)
    {
        var health = t.GetComponentInParent<PlayerHealth>();
        if (health != null && health.character != null &&
            !string.IsNullOrEmpty(health.character.displayName))
            return health.character.displayName;
        return t.root != null ? t.root.name : t.name;
    }

    static void AddFromTransform(List<StateEntry> list, HashSet<string> seen, Transform body, string characterLabel)
    {
        var animators = body.GetComponentsInChildren<Animator>(true);
        for (int i = 0; i < animators.Length; i++)
            AddFromAnimator(list, seen, animators[i], characterLabel);
    }

    static void AddFromAnimator(List<StateEntry> list, HashSet<string> seen, Animator animator, string characterLabel)
    {
        if (animator == null || animator.runtimeAnimatorController == null)
            return;

        RuntimeAnimatorController rac = animator.runtimeAnimatorController;
        if (rac is AnimatorOverrideController ov && ov.runtimeAnimatorController != null)
            rac = ov.runtimeAnimatorController;

        var path = AssetDatabase.GetAssetPath(rac);
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        if (controller == null)
            return;
        AddFromController(list, seen, controller, characterLabel);
    }

    static void AddFromProjectControllers(List<StateEntry> list, HashSet<string> seen)
    {
        string[] guids = AssetDatabase.FindAssets("t:AnimatorController");
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (string.IsNullOrEmpty(path) || path.Contains("Third Party") || path.Contains("TextMesh Pro"))
                continue;
            if (!path.Contains("Animations") && !path.Contains("Player") && !path.Contains("Ax") &&
                !path.Contains("Jyn"))
                continue;

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller == null)
                continue;

            string label = controller.name.Replace("Player", "").Replace("Controller", "").Trim();
            if (string.IsNullOrEmpty(label))
                label = controller.name;
            AddFromController(list, seen, controller, label);
        }
    }

    static void AddFromController(
        List<StateEntry> list, HashSet<string> seen, AnimatorController controller, string characterLabel)
    {
        if (controller?.layers == null)
            return;
        for (int li = 0; li < controller.layers.Length; li++)
            CollectStates(controller.layers[li].stateMachine, list, seen, characterLabel);
    }

    static void CollectStates(
        AnimatorStateMachine sm, List<StateEntry> list, HashSet<string> seen, string characterLabel)
    {
        if (sm == null)
            return;
        for (int i = 0; i < sm.states.Length; i++)
        {
            var st = sm.states[i].state;
            if (st == null || string.IsNullOrEmpty(st.name))
                continue;
            string key = characterLabel + "|" + st.name;
            if (!seen.Add(key))
                continue;
            list.Add(new StateEntry { characterLabel = characterLabel, stateName = st.name });
        }

        for (int i = 0; i < sm.stateMachines.Length; i++)
            CollectStates(sm.stateMachines[i].stateMachine, list, seen, characterLabel);
    }
}
