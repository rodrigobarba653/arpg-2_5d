using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Script-style Dialogue Editor. Per-line speaker, scene binding, portrait, text, choices.
/// Dialogue Asset is the source of truth; EventRunner only starts/waits/cancels.
/// </summary>
public sealed class DialogueEditorWindow : EditorWindow
{
    const string MenuPath = "Tools/ARPG/Dialogue/Dialogue Editor";
    const string JynId = "jyn";
    const string AxId = "ax";

    DialogueAsset asset;
    SerializedObject so;
    Vector2 scroll;
    string createName = "GuardEncounter";
    string createId = "guard_encounter";
    bool showAdvancedIds;
    bool showHelp = true;
    CharacterPortraitRoster roster;
    readonly List<DialogueValidationIssue> issues = new List<DialogueValidationIssue>();
    GUIStyle cardTitle;
    GUIStyle hintStyle;
    GUIStyle bigText;
    bool stylesReady;

    [MenuItem(MenuPath, false, 180)]
    public static void Open()
    {
        var w = GetWindow<DialogueEditorWindow>();
        w.titleContent = new GUIContent("Dialogue Editor");
        w.minSize = new Vector2(540f, 520f);
        w.Show();
    }

    public static void OpenAsset(DialogueAsset target)
    {
        Open();
        GetWindow<DialogueEditorWindow>().SetAsset(target);
    }

    void OnEnable()
    {
        roster = DialogueAuthoringOps.FindAnyRoster();
        if (asset == null && Selection.activeObject is DialogueAsset selected)
            SetAsset(selected);
        else if (asset != null)
            Bind();
    }

    void OnSelectionChange()
    {
        if (Selection.activeObject is DialogueAsset selected && selected != asset)
            SetAsset(selected);
        Repaint();
    }

    void OnFocus()
    {
        roster = DialogueAuthoringOps.FindAnyRoster();
        if (asset != null)
            RefreshIssues();
        Repaint();
    }

    void OnLostFocus() => FlushSave();

    void SetAsset(DialogueAsset next)
    {
        FlushSave();
        asset = next;
        Bind();
        RefreshIssues();
        Repaint();
    }

    void Bind() => so = asset != null ? new SerializedObject(asset) : null;

    void EnsureStyles()
    {
        if (stylesReady)
            return;
        cardTitle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 13 };
        hintStyle = new GUIStyle(EditorStyles.miniLabel) { wordWrap = true };
        bigText = new GUIStyle(EditorStyles.textArea) { wordWrap = true, fontSize = 13 };
        stylesReady = true;
    }

    void OnGUI()
    {
        EnsureStyles();
        DrawToolbar();

        if (asset == null)
        {
            DrawEmpty();
            return;
        }

        if (so == null)
            Bind();

        so.Update();
        DrawHeader();

        if (showHelp)
        {
            EditorGUILayout.HelpBox(
                "Each card = one line. Pick who speaks (Player or a scene NPC). " +
                "Portraits: Use Default / Custom / Hidden. " +
                "Chatboxes (top): Pin On Show + Bubble Lifetime (One At A Time = one bubble; previous closes). " +
                "EventRunner → Start Dialogue only needs the Dialogue Asset.",
                MessageType.Info);
        }

        scroll = EditorGUILayout.BeginScrollView(scroll);
        SerializedProperty nodes = so.FindProperty("nodes");
        int visible = 0;
        for (int i = 0; i < nodes.arraySize; i++)
        {
            SerializedProperty element = nodes.GetArrayElementAtIndex(i);
            object value = element.managedReferenceValue;
            if (value is DialogueEndNode)
                continue;

            visible++;
            if (value is DialogueLineNode)
                DrawLineCard(visible, i, element);
            else if (value is DialogueChoiceNode choice)
                DrawChoiceCard(visible, i, element, choice);
            else if (value is DialogueConditionNode)
                DrawGateCard(visible, i, element);

            DrawConnector();
        }

        DrawAddButtons();
        EditorGUILayout.Space(8f);
        DrawValidation();
        EditorGUILayout.EndScrollView();

        if (so.ApplyModifiedProperties())
        {
            EditorUtility.SetDirty(asset);
            RefreshIssues();
        }
    }

    void DrawToolbar()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            if (GUILayout.Button("New", EditorStyles.toolbarButton, GUILayout.Width(44f)))
                CreateNew();
            if (GUILayout.Button("Open…", EditorStyles.toolbarButton, GUILayout.Width(52f)))
                OpenPicker();
            using (new EditorGUI.DisabledScope(asset == null))
            {
                if (GUILayout.Button("Save", EditorStyles.toolbarButton, GUILayout.Width(44f)))
                    FlushSave();
                if (GUILayout.Button("Ping", EditorStyles.toolbarButton, GUILayout.Width(40f)))
                    EditorGUIUtility.PingObject(asset);
            }
            GUILayout.FlexibleSpace();
            showAdvancedIds = GUILayout.Toggle(showAdvancedIds, "IDs", EditorStyles.toolbarButton, GUILayout.Width(36f));
            showHelp = GUILayout.Toggle(showHelp, "Help", EditorStyles.toolbarButton, GUILayout.Width(44f));
        }

        // Always-visible conversation presentation (not buried in line cards).
        if (asset != null)
            DrawChatboxSettingsBar();
    }

    void DrawChatboxSettingsBar()
    {
        using (new EditorGUILayout.VerticalScope("box"))
        {
            EditorGUILayout.LabelField("Chatboxes", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            bool pin = EditorGUILayout.Toggle(
                new GUIContent("Pin On Show", "Freeze bubble screen position when it appears."),
                asset.pinBubblesOnShow);
            var lifetime = (DialogueBubbleLifetime)EditorGUILayout.EnumPopup(
                new GUIContent(
                    "Bubble Lifetime",
                    "Per Speaker Sticky / One At A Time / Keep Recent"),
                asset.bubbleLifetime);
            int keep = asset.keepRecentCount;
            if (lifetime == DialogueBubbleLifetime.KeepRecent)
            {
                keep = EditorGUILayout.IntField(
                    new GUIContent("Keep Recent Count", "How many speakers stay visible."),
                    keep);
                keep = Mathf.Max(1, keep);
            }

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(asset, "Dialogue Chatbox Settings");
                asset.pinBubblesOnShow = pin;
                asset.bubbleLifetime = lifetime;
                asset.keepRecentCount = keep;
                EditorUtility.SetDirty(asset);
                if (so != null)
                    so.Update();
            }

            if (lifetime == DialogueBubbleLifetime.OneAtATime)
            {
                EditorGUILayout.HelpBox(
                    "One At A Time: cada línea cierra el bubble anterior antes de abrir el siguiente.",
                    MessageType.Info);
            }
        }
    }

    void DrawEmpty()
    {
        EditorGUILayout.Space(16f);
        EditorGUILayout.LabelField("Dialogue Editor", EditorStyles.boldLabel);
        createName = EditorGUILayout.TextField("Name", createName);
        createId = EditorGUILayout.TextField("Id", createId);
        if (GUILayout.Button("Create Conversation", GUILayout.Height(30f)))
            CreateNew();
        if (GUILayout.Button("Open Existing…"))
            OpenPicker();
    }

    void DrawHeader()
    {
        using (new EditorGUILayout.VerticalScope("box"))
        {
            EditorGUILayout.LabelField(asset.name, cardTitle);
            SerializedProperty id = so.FindProperty("dialogueId");
            if (id != null)
                EditorGUILayout.PropertyField(id, new GUIContent("Dialogue Id"));
            SerializedProperty lockGp = so.FindProperty("lockGameplay");
            if (lockGp != null)
                EditorGUILayout.PropertyField(lockGp, new GUIContent("Lock Gameplay"));
        }
    }

    void DrawConnector()
    {
        Rect rect = GUILayoutUtility.GetRect(16f, 16f);
        GUI.Label(rect, "↓", new GUIStyle(EditorStyles.centeredGreyMiniLabel) { alignment = TextAnchor.MiddleCenter });
    }

    void DrawLineCard(int number, int index, SerializedProperty element)
    {
        using (new EditorGUILayout.VerticalScope("box"))
        {
            DrawCardChrome(number, index, "Dialogue Line", element);

            SerializedProperty speakerId = element.FindPropertyRelative("speakerId");
            SerializedProperty displayName = element.FindPropertyRelative("speakerDisplayName");
            SerializedProperty text = element.FindPropertyRelative("text");
            SerializedProperty variants = element.FindPropertyRelative("textVariants");
            SerializedProperty portraitMode = element.FindPropertyRelative("portraitMode");
            SerializedProperty portraitSprite = element.FindPropertyRelative("portraitSprite");
            SerializedProperty portraitVariants = element.FindPropertyRelative("portraitVariants");
            SerializedProperty next = element.FindPropertyRelative("nextNodeId");

            bool isPlayer = DialogueActiveCharacter.IsActivePlayerAlias(speakerId.stringValue);
            DrawSpeakerRow(speakerId, displayName, ref isPlayer);
            DrawPortraitRow(isPlayer, speakerId.stringValue, portraitMode, portraitSprite, portraitVariants);

            if (isPlayer)
            {
                EditorGUILayout.LabelField("Text (shared fallback)", EditorStyles.miniBoldLabel);
                text.stringValue = EditorGUILayout.TextArea(text.stringValue ?? "", bigText, GUILayout.MinHeight(40f));

                EditorGUILayout.LabelField("If playing as Jyn (optional)", EditorStyles.miniLabel);
                string jyn = GetTextVariant(variants, JynId);
                string nextJyn = EditorGUILayout.TextArea(jyn, bigText, GUILayout.MinHeight(40f));
                EditorGUILayout.LabelField("If playing as Ax (optional)", EditorStyles.miniLabel);
                string ax = GetTextVariant(variants, AxId);
                string nextAx = EditorGUILayout.TextArea(ax, bigText, GUILayout.MinHeight(40f));
                if (nextJyn != jyn || nextAx != ax)
                {
                    SetTextVariant(variants, JynId, nextJyn);
                    SetTextVariant(variants, AxId, nextAx);
                }
            }
            else
            {
                EditorGUILayout.LabelField("Text", EditorStyles.miniBoldLabel);
                text.stringValue = EditorGUILayout.TextArea(text.stringValue ?? "", bigText, GUILayout.MinHeight(56f));
            }

            DrawVoiceRow(element);

            DrawDestination(next, "Next");
        }
    }

    void DrawVoiceRow(SerializedProperty element)
    {
        SerializedProperty voiceClip = element.FindPropertyRelative("voiceClip");
        SerializedProperty voiceVolume = element.FindPropertyRelative("voiceVolume");
        if (voiceClip == null)
            return;

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Voice / Audio (sync with this box)", EditorStyles.miniBoldLabel);
        EditorGUILayout.PropertyField(voiceClip, new GUIContent("Clip", "Plays when this line starts; stops on Continue / next line / cancel."));
        if (voiceClip.objectReferenceValue != null && voiceVolume != null)
            EditorGUILayout.Slider(voiceVolume, 0f, 1f, new GUIContent("Volume"));
    }

    void DrawSpeakerRow(SerializedProperty speakerId, SerializedProperty displayName, ref bool isPlayer)
    {
        int mode = isPlayer ? 0 : 1;
        int nextMode = EditorGUILayout.Popup("Speaker", mode, new[] { "Active Player", "NPC (scene character)" });
        if (nextMode != mode)
        {
            if (nextMode == 0)
            {
                speakerId.stringValue = DialogueAuthoringOps.ActivePlayerSpeakerId;
                displayName.stringValue = "";
                isPlayer = true;
            }
            else
            {
                speakerId.stringValue = "";
                isPlayer = false;
            }
        }
        else
        {
            isPlayer = nextMode == 0;
        }

        if (isPlayer)
        {
            speakerId.stringValue = DialogueAuthoringOps.ActivePlayerSpeakerId;
            EditorGUILayout.LabelField("Scene Character", "Active Player (Jyn / Ax automatically)");
            return;
        }

        DialogueSpeakerBinding current = DialogueSpeakerBindingLookup.Find(speakerId.stringValue);
        Transform currentTf = current != null ? current.transform : null;
        Transform picked = (Transform)EditorGUILayout.ObjectField(
            new GUIContent("Scene Character", "Pick the NPC instance in the open scene. A Speaker Binding is added if missing."),
            currentTf,
            typeof(Transform),
            true);

        if (picked != currentTf && picked != null)
        {
            var binding = EnsureBinding(picked);
            speakerId.stringValue = binding.speakerId;
            if (string.IsNullOrEmpty(displayName.stringValue))
                displayName.stringValue = binding.ResolvedDisplayName;
        }

        if (current != null)
        {
            EditorGUILayout.LabelField(" ", current.ResolvedDisplayName + "  ·  id " + current.speakerId, hintStyle);
            Sprite def = current.defaultPortrait;
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel("NPC Default Portrait");
                Sprite nextPortrait = (Sprite)EditorGUILayout.ObjectField(def, typeof(Sprite), false);
                if (nextPortrait != def)
                {
                    Undo.RecordObject(current, "NPC Default Portrait");
                    current.defaultPortrait = nextPortrait;
                    EditorUtility.SetDirty(current);
                }
            }
        }
        else if (string.IsNullOrEmpty(speakerId.stringValue))
        {
            EditorGUILayout.HelpBox("Pick a Scene Character (e.g. Soldier-Melee 1).", MessageType.Warning);
        }
        else
        {
            EditorGUILayout.HelpBox(
                "Speaker id '" + speakerId.stringValue + "' has no DialogueSpeakerBinding in the open scenes. Pick the NPC above.",
                MessageType.Warning);
        }

        if (showAdvancedIds)
        {
            EditorGUILayout.PropertyField(speakerId, new GUIContent("Speaker Id (advanced)"));
            EditorGUILayout.PropertyField(displayName, new GUIContent("Display Name Override"));
        }
    }

    void DrawPortraitRow(
        bool isPlayer,
        string speakerIdValue,
        SerializedProperty portraitMode,
        SerializedProperty portraitSprite,
        SerializedProperty portraitVariants)
    {
        portraitMode.enumValueIndex = (int)(DialoguePortraitMode)EditorGUILayout.EnumPopup(
            "Portrait",
            (DialoguePortraitMode)portraitMode.enumValueIndex);

        var mode = (DialoguePortraitMode)portraitMode.enumValueIndex;
        if (mode == DialoguePortraitMode.Hidden)
        {
            EditorGUILayout.LabelField(" ", "No portrait on this line.", hintStyle);
            return;
        }

        if (mode == DialoguePortraitMode.UseDefault)
        {
            if (isPlayer)
            {
                EditorGUILayout.LabelField(" ", "Uses roster defaults for Jyn / Ax.", hintStyle);
                DrawThumbRow(RosterPortrait(JynId), RosterPortrait(AxId), "Jyn default", "Ax default");
            }
            else
            {
                Sprite npcDefault = DialogueSpeakerBindingLookup.FindDefaultPortrait(speakerIdValue);
                if (npcDefault == null)
                    npcDefault = RosterPortrait(speakerIdValue);
                DrawThumbRow(npcDefault, null, "NPC default", null);
                if (npcDefault == null)
                    EditorGUILayout.LabelField(" ", "Assign NPC Default Portrait on the scene binding (above).", hintStyle);
            }
            return;
        }

        // Custom
        if (isPlayer)
        {
            EditorGUILayout.LabelField("Custom portraits (optional per hero)", hintStyle);
            DrawCustomPlayerPortrait(portraitVariants, JynId, "Jyn");
            DrawCustomPlayerPortrait(portraitVariants, AxId, "Ax");
        }
        else
        {
            Sprite current = portraitSprite.objectReferenceValue as Sprite;
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawPortraitThumb(current);
                portraitSprite.objectReferenceValue = EditorGUILayout.ObjectField(
                    "Custom Portrait",
                    current,
                    typeof(Sprite),
                    false);
            }
        }
    }

    void DrawCustomPlayerPortrait(SerializedProperty variants, string characterId, string label)
    {
        Sprite current = GetPortraitVariant(variants, characterId);
        using (new EditorGUILayout.HorizontalScope())
        {
            DrawPortraitThumb(current);
            Sprite next = (Sprite)EditorGUILayout.ObjectField(label, current, typeof(Sprite), false);
            if (next != current)
            {
                if (next == null)
                    ClearPortraitVariant(variants, characterId);
                else
                    SetPortraitVariant(variants, characterId, next);
            }
        }
    }

    void DrawChoiceCard(int number, int index, SerializedProperty element, DialogueChoiceNode live)
    {
        using (new EditorGUILayout.VerticalScope("box"))
        {
            DrawCardChrome(number, index, "Player Choice", element);
            SerializedProperty prompt = element.FindPropertyRelative("promptText");
            EditorGUILayout.LabelField("Question (optional)", EditorStyles.miniBoldLabel);
            prompt.stringValue = EditorGUILayout.TextArea(prompt.stringValue ?? "", bigText, GUILayout.MinHeight(32f));

            SerializedProperty options = element.FindPropertyRelative("options");
            for (int o = 0; o < options.arraySize; o++)
            {
                SerializedProperty option = options.GetArrayElementAtIndex(o);
                using (new EditorGUILayout.VerticalScope("helpbox"))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField("Option " + (char)('A' + Mathf.Min(o, 25)), EditorStyles.boldLabel);
                        if (GUILayout.Button("✕", GUILayout.Width(22f)))
                        {
                            options.DeleteArrayElementAtIndex(o);
                            break;
                        }
                    }

                    SerializedProperty optText = option.FindPropertyRelative("text");
                    EditorGUILayout.LabelField("What the player says", EditorStyles.miniLabel);
                    optText.stringValue = EditorGUILayout.TextArea(optText.stringValue ?? "", bigText, GUILayout.MinHeight(36f));

                    DrawDestination(option.FindPropertyRelative("nextNodeId"), "Continue to");

                    SerializedProperty remember = option.FindPropertyRelative("rememberChoice");
                    SerializedProperty rememberAs = option.FindPropertyRelative("rememberAs");
                    SerializedProperty rememberFlagId = option.FindPropertyRelative("rememberFlagId");

                    remember.boolValue = EditorGUILayout.ToggleLeft(
                        new GUIContent(
                            "Remember this choice",
                            "Stores a story fact in WorldState when the player confirms. " +
                            "Not a quest or reward by itself. SaveManager writes it to disk on the next save — not instantly."),
                        remember.boolValue);

                    if (remember.boolValue)
                    {
                        if (string.IsNullOrEmpty(rememberAs.stringValue) && !string.IsNullOrEmpty(optText.stringValue))
                            rememberAs.stringValue = optText.stringValue;

                        rememberAs.stringValue = EditorGUILayout.TextField(
                            new GUIContent(
                                "Remember as",
                                "Plain-language fact. The WorldState flag id is the slug of this text " +
                                "(e.g. \"Option A Picked\" → option_a_picked)."),
                            rememberAs.stringValue ?? "");

                        string slug = DialogueRememberFlag.Sanitize(rememberAs.stringValue);
                        if (showAdvancedIds)
                        {
                            if (string.IsNullOrEmpty(rememberFlagId.stringValue) && !string.IsNullOrEmpty(slug))
                                rememberFlagId.stringValue = slug;

                            using (new EditorGUILayout.HorizontalScope())
                            {
                                EditorGUILayout.PropertyField(
                                    rememberFlagId,
                                    new GUIContent(
                                        "Flag Id (advanced)",
                                        "Stable WorldState id. Leave empty / match slug unless you need a custom id."));
                                string copyId = rememberFlagId.stringValue;
                                using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(copyId)))
                                {
                                    if (GUILayout.Button(
                                            new GUIContent("Copy", "Copy flag id to clipboard"),
                                            EditorStyles.miniButton,
                                            GUILayout.Width(48f)))
                                    {
                                        EditorGUIUtility.systemCopyBuffer = copyId;
                                        ShowNotification(new GUIContent("Copied: " + copyId));
                                    }
                                }
                            }
                        }
                        else
                        {
                            // Non-advanced: flag id always follows the label slug (fixes stale ids like "o").
                            if (!string.IsNullOrEmpty(slug))
                                rememberFlagId.stringValue = slug;

                            using (new EditorGUILayout.HorizontalScope())
                            {
                                EditorGUILayout.LabelField(
                                    "Flag id",
                                    string.IsNullOrEmpty(slug)
                                        ? "(type Remember as first)"
                                        : slug,
                                    hintStyle);
                                using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(slug)))
                                {
                                    if (GUILayout.Button(
                                            new GUIContent("Copy", "Copy flag id to clipboard"),
                                            EditorStyles.miniButton,
                                            GUILayout.Width(48f)))
                                    {
                                        EditorGUIUtility.systemCopyBuffer = slug;
                                        ShowNotification(new GUIContent("Copied: " + slug));
                                    }
                                }
                            }
                        }

                        EditorGUILayout.HelpBox(
                            "Remember = WorldState flag (true/false). Use this Flag id in FlagDrivenActive / conditions. " +
                            "Separate from Continue to and from immediate actions below.",
                            MessageType.None);
                    }

                    SerializedProperty consequences = option.FindPropertyRelative("consequences");
                    EditorGUILayout.LabelField("Immediate action (optional)", EditorStyles.miniBoldLabel);
                    EditorGUILayout.LabelField("Runs once when this option is confirmed (open door, give item, set flag, …).", hintStyle);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("None / clear", EditorStyles.miniButton))
                        {
                            consequences.ClearArray();
                        }
                        if (GUILayout.Button("Add action…", EditorStyles.miniButton))
                            ShowConsequenceMenu(live, o);
                    }
                    if (consequences.arraySize > 0)
                        EditorGUILayout.PropertyField(consequences, GUIContent.none, true);

                    if (showAdvancedIds)
                        EditorGUILayout.PropertyField(option.FindPropertyRelative("optionId"));
                }
            }

            if (GUILayout.Button("+ Add option"))
            {
                so.ApplyModifiedProperties();
                DialogueAuthoringOps.AddOption(asset, live, false);
                Bind();
                GUIUtility.ExitGUI();
            }
        }
    }

    void ShowConsequenceMenu(DialogueChoiceNode choice, int optionIndex)
    {
        var menu = new GenericMenu();
        foreach (var entry in EventActionRegistry.ForContext(EventActionRegistry.AuthorContext.DialogueConsequence))
        {
            Type type = entry.Type;
            string path = entry.Category + "/" + entry.MenuName;
            menu.AddItem(new GUIContent(path), false, () =>
            {
                so.ApplyModifiedProperties();
                EventAction instance = (EventAction)Activator.CreateInstance(type);
                DialogueAuthoringOps.AddConsequence(asset, choice.options[optionIndex], instance);
                Bind();
                RefreshIssues();
            });
        }
        menu.ShowAsContext();
    }

    void DrawGateCard(int number, int index, SerializedProperty element)
    {
        using (new EditorGUILayout.VerticalScope("box"))
        {
            DrawCardChrome(number, index, "Condition Gate", element);
            EditorGUILayout.PropertyField(element.FindPropertyRelative("conditions"), true);
            DrawDestination(element.FindPropertyRelative("trueNodeId"), "If true →");
            DrawDestination(element.FindPropertyRelative("falseNodeId"), "If false →");
        }
    }

    void DrawCardChrome(int number, int index, string kind, SerializedProperty element)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField(number + ".  " + kind, cardTitle);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("↑", EditorStyles.miniButtonLeft, GUILayout.Width(22f)))
            {
                so.ApplyModifiedProperties();
                DialogueAuthoringOps.MoveNode(asset, index, Mathf.Max(0, index - 1));
                Bind();
                RelinkLinear();
                GUIUtility.ExitGUI();
            }
            if (GUILayout.Button("↓", EditorStyles.miniButtonMid, GUILayout.Width(22f)))
            {
                so.ApplyModifiedProperties();
                DialogueAuthoringOps.MoveNode(asset, index, Mathf.Min(asset.nodes.Count - 1, index + 1));
                Bind();
                RelinkLinear();
                GUIUtility.ExitGUI();
            }
            if (GUILayout.Button("✕", EditorStyles.miniButtonRight, GUILayout.Width(22f)))
            {
                if (EditorUtility.DisplayDialog("Remove", "Remove this card?", "Remove", "Cancel"))
                {
                    so.ApplyModifiedProperties();
                    DialogueAuthoringOps.RemoveNode(asset, asset.nodes[index]);
                    Bind();
                    RelinkLinear();
                    GUIUtility.ExitGUI();
                }
            }
        }

        if (showAdvancedIds)
        {
            SerializedProperty nodeId = element.FindPropertyRelative("nodeId");
            if (nodeId != null)
                EditorGUILayout.PropertyField(nodeId);
        }
    }

    void DrawDestination(SerializedProperty property, string label)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.PrefixLabel(label);
            string shown = FriendlyDest(property.stringValue);
            EditorGUILayout.LabelField(shown);
            if (EditorGUILayout.DropdownButton(new GUIContent("Pick"), FocusType.Passive, GUILayout.Width(48f)))
            {
                string path = property.propertyPath;
                var menu = new GenericMenu();
                menu.AddItem(new GUIContent("(End conversation)"), false, () => AssignDest(path, ""));
                menu.AddSeparator("");
                if (asset.nodes != null)
                {
                    int n = 0;
                    for (int i = 0; i < asset.nodes.Count; i++)
                    {
                        DialogueNode node = asset.nodes[i];
                        if (node == null || node is DialogueEndNode || string.IsNullOrEmpty(node.nodeId))
                            continue;
                        n++;
                        string id = node.nodeId;
                        string title = n + ". " + (node is DialogueLineNode line
                            ? Truncate(line.text, 28)
                            : node is DialogueChoiceNode ? "Choice"
                            : node.nodeId);
                        menu.AddItem(new GUIContent(title + "  [" + id + "]"), false, () => AssignDest(path, id));
                    }
                }
                menu.ShowAsContext();
            }
        }
        if (showAdvancedIds)
            EditorGUILayout.PropertyField(property, new GUIContent("Node Id"));
    }

    string FriendlyDest(string id)
    {
        if (string.IsNullOrEmpty(id))
            return "(End conversation)";
        DialogueNode node = asset.FindNode(id);
        if (node is DialogueLineNode line)
            return Truncate(string.IsNullOrEmpty(line.text) ? id : line.text, 40);
        if (node is DialogueChoiceNode)
            return "Choice · " + id;
        if (node is DialogueEndNode)
            return "End";
        return string.IsNullOrEmpty(id) ? "(End)" : id;
    }

    void AssignDest(string propertyPath, string id)
    {
        so.Update();
        SerializedProperty p = so.FindProperty(propertyPath);
        if (p != null)
            p.stringValue = id;
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(asset);
        RefreshIssues();
    }

    void DrawAddButtons()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("+ Dialogue Line", GUILayout.Height(28f)))
            {
                so.ApplyModifiedProperties();
                DialogueAuthoringOps.AddLine(asset, afterNodeId: LastScriptNodeId());
                Bind();
                RelinkLinear();
                GUIUtility.ExitGUI();
            }
            if (GUILayout.Button("+ Player Choice", GUILayout.Height(28f)))
            {
                so.ApplyModifiedProperties();
                var choice = DialogueAuthoringOps.AddChoice(asset, LastScriptNodeId());
                if (choice != null && (choice.options == null || choice.options.Count == 0))
                {
                    DialogueAuthoringOps.AddOption(asset, choice, false);
                    DialogueAuthoringOps.AddOption(asset, choice, false);
                }
                Bind();
                RelinkLinear();
                GUIUtility.ExitGUI();
            }
        }
    }

    void DrawValidation()
    {
        using (new EditorGUILayout.VerticalScope("box"))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Check", EditorStyles.boldLabel);
                if (GUILayout.Button("Refresh", EditorStyles.miniButton, GUILayout.Width(60f)))
                    RefreshIssues();
            }
            if (issues.Count == 0)
                EditorGUILayout.HelpBox("OK", MessageType.Info);
            else
            {
                for (int i = 0; i < Mathf.Min(issues.Count, 6); i++)
                    EditorGUILayout.LabelField("• " + issues[i].Location + ": " + issues[i].Problem, hintStyle);
            }
        }
    }

    static DialogueSpeakerBinding EnsureBinding(Transform target)
    {
        var binding = target.GetComponent<DialogueSpeakerBinding>();
        if (binding == null)
            binding = target.GetComponentInParent<DialogueSpeakerBinding>();
        if (binding == null)
        {
            binding = Undo.AddComponent<DialogueSpeakerBinding>(target.gameObject);
            binding.EnsureSpeakerId();
            binding.displayName = target.gameObject.name;
            EditorUtility.SetDirty(binding);
        }
        else
        {
            binding.EnsureSpeakerId();
        }
        return binding;
    }

    void DrawThumbRow(Sprite a, Sprite b, string labelA, string labelB)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            DrawPortraitThumb(a);
            EditorGUILayout.LabelField(labelA ?? "", hintStyle);
            if (b != null || labelB != null)
            {
                DrawPortraitThumb(b);
                EditorGUILayout.LabelField(labelB ?? "", hintStyle);
            }
        }
    }

    void DrawPortraitThumb(Sprite sprite)
    {
        Rect r = GUILayoutUtility.GetRect(48f, 48f, GUILayout.Width(48f), GUILayout.Height(48f));
        EditorGUI.DrawRect(r, new Color(0.15f, 0.15f, 0.15f, 0.35f));
        if (sprite == null || sprite.texture == null)
        {
            GUI.Label(r, "—", EditorStyles.centeredGreyMiniLabel);
            return;
        }
        Rect tr = sprite.textureRect;
        var uv = new Rect(
            tr.x / sprite.texture.width,
            tr.y / sprite.texture.height,
            tr.width / sprite.texture.width,
            tr.height / sprite.texture.height);
        GUI.DrawTextureWithTexCoords(r, sprite.texture, uv);
    }

    Sprite RosterPortrait(string characterId)
    {
        if (roster == null || string.IsNullOrEmpty(characterId))
            return null;
        return roster.FindPortrait(characterId, "Neutral");
    }

    string LastScriptNodeId()
    {
        if (asset?.nodes == null)
            return null;
        for (int i = asset.nodes.Count - 1; i >= 0; i--)
        {
            DialogueNode n = asset.nodes[i];
            if (n != null && !(n is DialogueEndNode))
                return n.nodeId;
        }
        return null;
    }

    void RelinkLinear()
    {
        DialogueAuthoringOps.Record(asset, "Relink Dialogue");
        DialogueLineNode prev = null;
        DialogueEndNode end = null;
        for (int i = 0; i < asset.nodes.Count; i++)
        {
            if (asset.nodes[i] is DialogueEndNode e)
                end = e;
        }
        if (end == null)
            end = DialogueAuthoringOps.AddEnd(asset);

        for (int i = 0; i < asset.nodes.Count; i++)
        {
            DialogueNode n = asset.nodes[i];
            if (n is DialogueLineNode line)
            {
                if (prev != null)
                    prev.nextNodeId = line.nodeId;
                prev = line;
            }
            else if (n is DialogueChoiceNode choice)
            {
                if (prev != null)
                    prev.nextNodeId = choice.nodeId;
                prev = null;
            }
            else if (n is DialogueConditionNode gate)
            {
                if (prev != null)
                    prev.nextNodeId = gate.nodeId;
                prev = null;
            }
        }
        if (prev != null && string.IsNullOrEmpty(prev.nextNodeId))
            prev.nextNodeId = end.nodeId;
        if (string.IsNullOrEmpty(asset.entryNodeId) || asset.FindNode(asset.entryNodeId) == null)
        {
            for (int i = 0; i < asset.nodes.Count; i++)
            {
                if (asset.nodes[i] != null && !(asset.nodes[i] is DialogueEndNode))
                {
                    asset.entryNodeId = asset.nodes[i].nodeId;
                    break;
                }
            }
        }
        EditorUtility.SetDirty(asset);
        Bind();
        RefreshIssues();
    }

    static string GetTextVariant(SerializedProperty variants, string characterId)
    {
        if (variants == null || !variants.isArray)
            return "";
        for (int i = 0; i < variants.arraySize; i++)
        {
            SerializedProperty v = variants.GetArrayElementAtIndex(i);
            if (string.Equals(v.FindPropertyRelative("characterId").stringValue, characterId, StringComparison.OrdinalIgnoreCase))
                return v.FindPropertyRelative("text").stringValue ?? "";
        }
        return "";
    }

    static void SetTextVariant(SerializedProperty variants, string characterId, string text)
    {
        if (variants == null || !variants.isArray)
            return;
        for (int i = 0; i < variants.arraySize; i++)
        {
            SerializedProperty v = variants.GetArrayElementAtIndex(i);
            if (string.Equals(v.FindPropertyRelative("characterId").stringValue, characterId, StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrEmpty(text))
                    variants.DeleteArrayElementAtIndex(i);
                else
                    v.FindPropertyRelative("text").stringValue = text;
                return;
            }
        }
        if (string.IsNullOrEmpty(text))
            return;
        int idx = variants.arraySize;
        variants.arraySize++;
        SerializedProperty created = variants.GetArrayElementAtIndex(idx);
        created.FindPropertyRelative("characterId").stringValue = characterId;
        created.FindPropertyRelative("text").stringValue = text;
    }

    static Sprite GetPortraitVariant(SerializedProperty variants, string characterId)
    {
        if (variants == null || !variants.isArray)
            return null;
        for (int i = 0; i < variants.arraySize; i++)
        {
            SerializedProperty v = variants.GetArrayElementAtIndex(i);
            if (string.Equals(v.FindPropertyRelative("characterId").stringValue, characterId, StringComparison.OrdinalIgnoreCase))
                return v.FindPropertyRelative("sprite").objectReferenceValue as Sprite;
        }
        return null;
    }

    static void SetPortraitVariant(SerializedProperty variants, string characterId, Sprite sprite)
    {
        for (int i = 0; i < variants.arraySize; i++)
        {
            SerializedProperty v = variants.GetArrayElementAtIndex(i);
            if (string.Equals(v.FindPropertyRelative("characterId").stringValue, characterId, StringComparison.OrdinalIgnoreCase))
            {
                v.FindPropertyRelative("sprite").objectReferenceValue = sprite;
                return;
            }
        }
        int idx = variants.arraySize;
        variants.arraySize++;
        SerializedProperty created = variants.GetArrayElementAtIndex(idx);
        created.FindPropertyRelative("characterId").stringValue = characterId;
        created.FindPropertyRelative("sprite").objectReferenceValue = sprite;
    }

    static void ClearPortraitVariant(SerializedProperty variants, string characterId)
    {
        for (int i = variants.arraySize - 1; i >= 0; i--)
        {
            SerializedProperty v = variants.GetArrayElementAtIndex(i);
            if (string.Equals(v.FindPropertyRelative("characterId").stringValue, characterId, StringComparison.OrdinalIgnoreCase))
                variants.DeleteArrayElementAtIndex(i);
        }
    }

    void CreateNew()
    {
        string name = string.IsNullOrWhiteSpace(createName) ? "NewConversation" : createName.Trim();
        string id = string.IsNullOrWhiteSpace(createId)
            ? DialogueAuthoringOps.SanitizeId(name)
            : DialogueAuthoringOps.SanitizeId(createId);
        SetAsset(DialogueAuthoringOps.CreateConversation(DialogueAuthoringOps.DefaultFolder, name, id));
    }

    void OpenPicker()
    {
        string path = EditorUtility.OpenFilePanel("Open Dialogue Asset", Application.dataPath + "/Game/Dialogue", "asset");
        if (string.IsNullOrEmpty(path))
            return;
        string normalized = path.Replace('\\', '/');
        string dataPath = Application.dataPath.Replace('\\', '/');
        if (!normalized.StartsWith(dataPath, StringComparison.Ordinal))
            return;
        string relative = "Assets" + normalized.Substring(dataPath.Length);
        var loaded = AssetDatabase.LoadAssetAtPath<DialogueAsset>(relative);
        if (loaded != null)
            SetAsset(loaded);
    }

    void FlushSave()
    {
        if (so != null)
            so.ApplyModifiedProperties();
        if (asset != null)
            EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();
        RefreshIssues();
    }

    void RefreshIssues() => DialogueAssetValidator.Collect(asset, issues);

    static string Truncate(string value, int max)
    {
        if (string.IsNullOrEmpty(value))
            return "";
        value = value.Replace('\n', ' ');
        return value.Length <= max ? value : value.Substring(0, max - 1) + "…";
    }
}
