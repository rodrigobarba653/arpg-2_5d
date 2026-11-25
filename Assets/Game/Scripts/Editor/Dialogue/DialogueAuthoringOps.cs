using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Mutation helpers for the Dialogue Editor. Writes the existing DialogueAsset schema only.
/// </summary>
public static class DialogueAuthoringOps
{
    public const string DefaultFolder = "Assets/Game/Dialogue";
    public const string ActivePlayerSpeakerId = DialogueActiveCharacter.ActivePlayerAlias;

    public static DialogueAsset CreateConversation(string folder, string fileName, string dialogueId)
    {
        if (string.IsNullOrEmpty(folder))
            folder = DefaultFolder;
        if (string.IsNullOrEmpty(fileName))
            fileName = "Dialogue";
        if (string.IsNullOrEmpty(dialogueId))
            dialogueId = SanitizeId(fileName);

        EnsureFolder(folder);

        string path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{fileName}.asset");
        var asset = ScriptableObject.CreateInstance<DialogueAsset>();
        asset.dialogueId = dialogueId;
        asset.lockGameplay = true;
        asset.defaultTypewriterCharsPerSecond = 48f;

        var line = new DialogueLineNode
        {
            nodeId = "line_1",
            speakerId = ActivePlayerSpeakerId,
            text = "",
            portraitExpression = "Neutral",
            nextNodeId = "end",
            textVariants = new List<DialogueTextVariant>
            {
                new DialogueTextVariant { characterId = "jyn", text = "" },
                new DialogueTextVariant { characterId = "ax", text = "" },
            },
        };
        var end = new DialogueEndNode { nodeId = "end" };
        asset.nodes = new List<DialogueNode> { line, end };
        asset.entryNodeId = line.nodeId;

        AssetDatabase.CreateAsset(asset, path);
        AssetDatabase.SaveAssets();
        EditorGUIUtility.PingObject(asset);
        Selection.activeObject = asset;
        return asset;
    }

    public static void Record(DialogueAsset asset, string undoLabel)
    {
        if (asset == null)
            return;
        Undo.RecordObject(asset, undoLabel);
        EditorUtility.SetDirty(asset);
    }

    public static string NextUniqueNodeId(DialogueAsset asset, string prefix)
    {
        if (string.IsNullOrEmpty(prefix))
            prefix = "node";
        var used = CollectIds(asset);
        int n = 1;
        string candidate;
        do
        {
            candidate = prefix + "_" + n;
            n++;
        } while (used.Contains(candidate));
        return candidate;
    }

    public static string NextUniqueOptionId(DialogueChoiceNode choice, string prefix = "opt")
    {
        var used = new HashSet<string>(StringComparer.Ordinal);
        if (choice?.options != null)
        {
            for (int i = 0; i < choice.options.Count; i++)
            {
                DialogueChoiceOption option = choice.options[i];
                if (option != null && !string.IsNullOrEmpty(option.optionId))
                    used.Add(option.optionId);
            }
        }

        int n = 1;
        string candidate;
        do
        {
            candidate = prefix + "_" + n;
            n++;
        } while (used.Contains(candidate));
        return candidate;
    }

    public static DialogueLineNode AddLine(DialogueAsset asset, int insertIndex = -1, string afterNodeId = null)
    {
        if (asset == null)
            return null;

        Record(asset, "Add Dialogue Line");
        EnsureNodes(asset);

        var line = new DialogueLineNode
        {
            nodeId = NextUniqueNodeId(asset, "line"),
            speakerId = ActivePlayerSpeakerId,
            text = "",
            portraitExpression = "Neutral",
            nextNodeId = "",
        };

        if (!string.IsNullOrEmpty(afterNodeId))
            WireAfter(asset, afterNodeId, line);
        else
            line.nextNodeId = "";

        InsertNode(asset, line, insertIndex);
        if (string.IsNullOrEmpty(asset.entryNodeId))
            asset.entryNodeId = line.nodeId;
        return line;
    }

    public static DialogueChoiceNode AddChoice(DialogueAsset asset, string afterNodeId = null)
    {
        if (asset == null)
            return null;

        Record(asset, "Add Dialogue Choice");
        EnsureNodes(asset);

        var choice = new DialogueChoiceNode
        {
            nodeId = NextUniqueNodeId(asset, "choice"),
            promptText = "",
            options = new List<DialogueChoiceOption>(),
            fallbackNodeId = "",
        };

        if (!string.IsNullOrEmpty(afterNodeId))
            WireAfter(asset, afterNodeId, choice);

        InsertNode(asset, choice, -1);
        return choice;
    }

    public static DialogueEndNode AddEnd(DialogueAsset asset)
    {
        if (asset == null)
            return null;

        Record(asset, "Add Dialogue End");
        EnsureNodes(asset);
        var end = new DialogueEndNode { nodeId = NextUniqueNodeId(asset, "end") };
        InsertNode(asset, end, -1);
        return end;
    }

    public static DialogueConditionNode AddCondition(DialogueAsset asset, string afterNodeId = null)
    {
        if (asset == null)
            return null;

        Record(asset, "Add Dialogue Condition");
        EnsureNodes(asset);
        var condition = new DialogueConditionNode
        {
            nodeId = NextUniqueNodeId(asset, "gate"),
            conditions = new List<EventCondition>(),
            trueNodeId = "",
            falseNodeId = "",
        };
        if (!string.IsNullOrEmpty(afterNodeId))
            WireAfter(asset, afterNodeId, condition);
        InsertNode(asset, condition, -1);
        return condition;
    }

    public static DialogueChoiceOption AddOption(DialogueAsset asset, DialogueChoiceNode choice, bool createFollowUpLine)
    {
        if (asset == null || choice == null)
            return null;

        Record(asset, "Add Choice Option");
        if (choice.options == null)
            choice.options = new List<DialogueChoiceOption>();

        string optionId = NextUniqueOptionId(choice);
        string nextId = "";
        if (createFollowUpLine)
        {
            var follow = new DialogueLineNode
            {
                nodeId = NextUniqueNodeId(asset, "line"),
                speakerId = ActivePlayerSpeakerId,
                text = "",
                portraitExpression = "Neutral",
                nextNodeId = "",
            };
            EnsureNodes(asset);
            InsertNode(asset, follow, -1);
            nextId = follow.nodeId;
        }

        var option = new DialogueChoiceOption
        {
            optionId = optionId,
            text = "Option",
            nextNodeId = nextId,
            consequences = new List<EventAction>(),
            availability = new List<EventCondition>(),
        };
        choice.options.Add(option);
        return option;
    }

    public static DialogueNode DuplicateNode(DialogueAsset asset, DialogueNode source)
    {
        if (asset == null || source == null)
            return null;

        Record(asset, "Duplicate Dialogue Node");
        EnsureNodes(asset);

        DialogueNode copy = CloneNode(source);
        if (copy == null)
            return null;

        string prefix = PrefixFor(copy);
        copy.nodeId = NextUniqueNodeId(asset, prefix);
        int index = asset.nodes.IndexOf(source);
        InsertNode(asset, copy, index >= 0 ? index + 1 : -1);
        return copy;
    }

    public static bool RemoveNode(DialogueAsset asset, DialogueNode node)
    {
        if (asset == null || node == null || asset.nodes == null)
            return false;

        Record(asset, "Remove Dialogue Node");
        string removedId = node.nodeId;
        asset.nodes.Remove(node);

        if (!string.IsNullOrEmpty(removedId))
            ClearReferencesTo(asset, removedId);

        if (asset.entryNodeId == removedId)
        {
            DialogueNode first = asset.nodes.Count > 0 ? asset.nodes[0] : null;
            asset.entryNodeId = first != null ? first.nodeId : "";
        }

        return true;
    }

    public static bool MoveNode(DialogueAsset asset, int from, int to)
    {
        if (asset?.nodes == null || from < 0 || from >= asset.nodes.Count || to < 0 || to >= asset.nodes.Count)
            return false;
        if (from == to)
            return false;

        Record(asset, "Reorder Dialogue Node");
        DialogueNode node = asset.nodes[from];
        asset.nodes.RemoveAt(from);
        asset.nodes.Insert(to, node);
        return true;
    }

    public static void LinkLinearSequence(DialogueAsset asset)
    {
        if (asset?.nodes == null || asset.nodes.Count == 0)
            return;

        Record(asset, "Link Linear Dialogue Sequence");
        DialogueLineNode previousLine = null;
        for (int i = 0; i < asset.nodes.Count; i++)
        {
            if (asset.nodes[i] is DialogueLineNode line)
            {
                if (previousLine != null)
                    previousLine.nextNodeId = line.nodeId;
                previousLine = line;
            }
            else if (asset.nodes[i] is DialogueChoiceNode || asset.nodes[i] is DialogueConditionNode)
            {
                // Stop auto-chaining across branches; author owns those links.
                previousLine = null;
            }
        }

        if (previousLine != null && string.IsNullOrEmpty(previousLine.nextNodeId))
        {
            DialogueEndNode end = FindFirstEnd(asset);
            if (end == null)
                end = AddEnd(asset);
            if (end != null)
                previousLine.nextNodeId = end.nodeId;
        }

        if (string.IsNullOrEmpty(asset.entryNodeId) && asset.nodes.Count > 0 && asset.nodes[0] != null)
            asset.entryNodeId = asset.nodes[0].nodeId;
    }

    public static void AddRememberChoiceConsequence(DialogueAsset asset, DialogueChoiceOption option)
    {
        if (asset == null || option == null)
            return;

        Record(asset, "Remember Choice");
        option.rememberChoice = true;
        if (string.IsNullOrEmpty(option.rememberAs))
            option.rememberAs = string.IsNullOrEmpty(option.text) ? "Chose " + option.optionId : option.text;
        if (string.IsNullOrEmpty(option.rememberFlagId))
            option.rememberFlagId = DialogueRememberFlag.ResolveId(asset, option);
    }

    public static string BuildRememberFlagId(DialogueAsset asset, DialogueChoiceOption option)
    {
        return DialogueRememberFlag.ResolveId(asset, option);
    }

    public static void AddConsequence(DialogueAsset asset, DialogueChoiceOption option, EventAction action)
    {
        if (asset == null || option == null || action == null)
            return;
        Record(asset, "Add Dialogue Consequence");
        if (option.consequences == null)
            option.consequences = new List<EventAction>();
        option.consequences.Add(action);
    }

    public static void RemoveConsequence(DialogueAsset asset, DialogueChoiceOption option, int index)
    {
        if (asset == null || option?.consequences == null)
            return;
        if (index < 0 || index >= option.consequences.Count)
            return;
        Record(asset, "Remove Dialogue Consequence");
        option.consequences.RemoveAt(index);
    }

    public static List<CharacterDefinition> LoadCharacterDefinitions()
    {
        var list = new List<CharacterDefinition>();
        string[] guids = AssetDatabase.FindAssets("t:CharacterDefinition");
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            var def = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(path);
            if (def != null)
                list.Add(def);
        }

        list.Sort((a, b) => string.Compare(a.displayName, b.displayName, StringComparison.OrdinalIgnoreCase));
        return list;
    }

    public static CharacterPortraitRoster FindAnyRoster()
    {
        string[] guids = AssetDatabase.FindAssets("t:CharacterPortraitRoster");
        if (guids == null || guids.Length == 0)
            return null;
        return AssetDatabase.LoadAssetAtPath<CharacterPortraitRoster>(AssetDatabase.GUIDToAssetPath(guids[0]));
    }

    public static string SanitizeId(string raw)
    {
        if (string.IsNullOrEmpty(raw))
            return "";
        string s = raw.Trim().ToLowerInvariant();
        s = Regex.Replace(s, @"[^a-z0-9_]+", "_");
        s = Regex.Replace(s, @"_+", "_").Trim('_');
        return s;
    }

    static void WireAfter(DialogueAsset asset, string afterNodeId, DialogueNode created)
    {
        DialogueNode after = asset.FindNode(afterNodeId);
        if (after is DialogueLineNode line)
        {
            string oldNext = line.nextNodeId;
            line.nextNodeId = created.nodeId;
            if (created is DialogueLineNode createdLine)
                createdLine.nextNodeId = oldNext;
            else if (created is DialogueConditionNode createdGate)
                createdGate.trueNodeId = oldNext;
            // Choice: author configures each option; oldNext is not auto-attached.
            return;
        }

        if (after is DialogueConditionNode condition)
        {
            if (string.IsNullOrEmpty(condition.trueNodeId))
                condition.trueNodeId = created.nodeId;
        }
    }

    static void InsertNode(DialogueAsset asset, DialogueNode node, int insertIndex)
    {
        if (insertIndex < 0 || insertIndex > asset.nodes.Count)
            asset.nodes.Add(node);
        else
            asset.nodes.Insert(insertIndex, node);
    }

    static void EnsureNodes(DialogueAsset asset)
    {
        if (asset.nodes == null)
            asset.nodes = new List<DialogueNode>();
    }

    static HashSet<string> CollectIds(DialogueAsset asset)
    {
        var used = new HashSet<string>(StringComparer.Ordinal);
        if (asset?.nodes == null)
            return used;
        for (int i = 0; i < asset.nodes.Count; i++)
        {
            DialogueNode node = asset.nodes[i];
            if (node != null && !string.IsNullOrEmpty(node.nodeId))
                used.Add(node.nodeId);
        }
        return used;
    }

    static void ClearReferencesTo(DialogueAsset asset, string removedId)
    {
        for (int i = 0; i < asset.nodes.Count; i++)
        {
            DialogueNode node = asset.nodes[i];
            if (node is DialogueLineNode line && line.nextNodeId == removedId)
                line.nextNodeId = "";
            else if (node is DialogueConditionNode condition)
            {
                if (condition.trueNodeId == removedId)
                    condition.trueNodeId = "";
                if (condition.falseNodeId == removedId)
                    condition.falseNodeId = "";
            }
            else if (node is DialogueChoiceNode choice)
            {
                if (choice.fallbackNodeId == removedId)
                    choice.fallbackNodeId = "";
                if (choice.options == null)
                    continue;
                for (int o = 0; o < choice.options.Count; o++)
                {
                    DialogueChoiceOption option = choice.options[o];
                    if (option != null && option.nextNodeId == removedId)
                        option.nextNodeId = "";
                }
            }
        }
    }

    static DialogueEndNode FindFirstEnd(DialogueAsset asset)
    {
        for (int i = 0; i < asset.nodes.Count; i++)
        {
            if (asset.nodes[i] is DialogueEndNode end)
                return end;
        }
        return null;
    }

    static string PrefixFor(DialogueNode node)
    {
        if (node is DialogueLineNode)
            return "line";
        if (node is DialogueChoiceNode)
            return "choice";
        if (node is DialogueConditionNode)
            return "gate";
        if (node is DialogueEndNode)
            return "end";
        return "node";
    }

    static DialogueNode CloneNode(DialogueNode source)
    {
        if (source is DialogueLineNode line)
        {
            return new DialogueLineNode
            {
                nodeId = line.nodeId,
                speakerId = line.speakerId,
                speakerDisplayName = line.speakerDisplayName,
                text = line.text,
                textVariants = CloneVariants(line.textVariants),
                portraitMode = line.portraitMode,
                portraitExpression = line.portraitExpression,
                portraitSprite = line.portraitSprite,
                portraitVariants = ClonePortraits(line.portraitVariants),
                nextNodeId = line.nextNodeId,
                typewriterCharsPerSecond = line.typewriterCharsPerSecond,
                ambientHoldSeconds = line.ambientHoldSeconds,
                voiceClip = line.voiceClip,
                voiceVolume = line.voiceVolume,
            };
        }

        if (source is DialogueChoiceNode choice)
        {
            var copy = new DialogueChoiceNode
            {
                nodeId = choice.nodeId,
                promptText = choice.promptText,
                fallbackNodeId = choice.fallbackNodeId,
                options = new List<DialogueChoiceOption>(),
            };
            if (choice.options != null)
            {
                for (int i = 0; i < choice.options.Count; i++)
                {
                    DialogueChoiceOption option = choice.options[i];
                    if (option == null)
                        continue;
                    copy.options.Add(new DialogueChoiceOption
                    {
                        optionId = option.optionId,
                        text = option.text,
                        textVariants = CloneVariants(option.textVariants),
                        nextNodeId = option.nextNodeId,
                        requiredCharacterId = option.requiredCharacterId,
                        availability = new List<EventCondition>(),
                        consequences = new List<EventAction>(),
                    });
                }
            }
            return copy;
        }

        if (source is DialogueConditionNode gate)
        {
            return new DialogueConditionNode
            {
                nodeId = gate.nodeId,
                trueNodeId = gate.trueNodeId,
                falseNodeId = gate.falseNodeId,
                conditions = new List<EventCondition>(),
            };
        }

        if (source is DialogueEndNode)
            return new DialogueEndNode { nodeId = source.nodeId };

        return null;
    }

    static List<DialogueTextVariant> CloneVariants(List<DialogueTextVariant> source)
    {
        var list = new List<DialogueTextVariant>();
        if (source == null)
            return list;
        for (int i = 0; i < source.Count; i++)
        {
            DialogueTextVariant v = source[i];
            if (v == null)
                continue;
            list.Add(new DialogueTextVariant { characterId = v.characterId, text = v.text });
        }
        return list;
    }

    static List<DialoguePortraitVariant> ClonePortraits(List<DialoguePortraitVariant> source)
    {
        var list = new List<DialoguePortraitVariant>();
        if (source == null)
            return list;
        for (int i = 0; i < source.Count; i++)
        {
            DialoguePortraitVariant v = source[i];
            if (v == null)
                continue;
            list.Add(new DialoguePortraitVariant { characterId = v.characterId, sprite = v.sprite });
        }
        return list;
    }

    static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder))
            return;

        string[] parts = folder.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }
}
