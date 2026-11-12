using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public enum DialogueValidationSeverity
{
    Info = 0,
    Warning = 1,
    Error = 2,
}

public struct DialogueValidationIssue
{
    public DialogueValidationSeverity Severity;
    public string Location;
    public string Problem;
    public string Correction;
}

/// <summary>
/// Editor-only checks for a <see cref="DialogueAsset"/>. Does not save, evaluate conditions, or touch WorldState.
/// </summary>
public static class DialogueAssetValidator
{
    public static void Collect(DialogueAsset asset, List<DialogueValidationIssue> into)
    {
        if (into == null)
            return;
        into.Clear();
        if (asset == null)
            return;

        if (string.IsNullOrEmpty(asset.dialogueId))
        {
            Add(into, DialogueValidationSeverity.Error, "Asset",
                "dialogueId is empty.",
                "Set a stable id. Saves and logs use it.");
        }

        var idOwners = new Dictionary<string, int>();
        var nodes = asset.nodes;
        if (nodes != null)
        {
            for (int i = 0; i < nodes.Count; i++)
            {
                DialogueNode node = nodes[i];
                if (node == null)
                {
                    Add(into, DialogueValidationSeverity.Error, "Index " + i,
                        "This entry is null.",
                        "Remove it or assign a Line, Choice, or End.");
                    continue;
                }

                if (string.IsNullOrEmpty(node.nodeId))
                {
                    Add(into, DialogueValidationSeverity.Error, "Index " + i,
                        TypeName(node) + " has an empty node id.",
                        "Set a node id. Other nodes cannot point at an empty id.");
                    continue;
                }

                if (idOwners.ContainsKey(node.nodeId))
                {
                    Add(into, DialogueValidationSeverity.Error, node.nodeId,
                        "Node id '" + node.nodeId + "' is duplicated (index " + idOwners[node.nodeId] + " and " + i + ").",
                        "Rename one of them. FindNode keeps the first match.");
                }
                else
                {
                    idOwners.Add(node.nodeId, i);
                }
            }
        }

        string resolvedEntry = asset.entryNodeId;
        if (string.IsNullOrEmpty(resolvedEntry))
        {
            DialogueNode first = nodes != null && nodes.Count > 0 ? nodes[0] : null;
            if (first == null || string.IsNullOrEmpty(first.nodeId))
            {
                Add(into, DialogueValidationSeverity.Error, "Asset",
                    "entryNodeId is empty and there is no first node to play.",
                    "Set entryNodeId to a node in this asset.");
            }
            else
            {
                resolvedEntry = first.nodeId;
                Add(into, DialogueValidationSeverity.Warning, "Asset",
                    "entryNodeId is empty. Playback starts at '" + first.nodeId + "'.",
                    "Set entryNodeId if that first node is not the start.");
            }
        }
        else if (asset.FindNode(resolvedEntry) == null)
        {
            Add(into, DialogueValidationSeverity.Error, resolvedEntry,
                "Entry '" + resolvedEntry + "' does not match any node id.",
                "Point entryNodeId at a node in this list, or create that node.");
            resolvedEntry = "";
        }

        var knownCharacters = LoadCharacterIds();

        if (nodes != null)
        {
            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i] is DialogueLineNode line)
                    ValidateLine(into, asset, line);
                else if (nodes[i] is DialogueChoiceNode choice)
                    ValidateChoice(into, asset, choice, knownCharacters);
                else if (nodes[i] is DialogueConditionNode condition)
                    ValidateCondition(into, asset, condition);
            }
        }

        if (!string.IsNullOrEmpty(resolvedEntry) && asset.FindNode(resolvedEntry) != null)
            ValidateReachable(into, asset, resolvedEntry, idOwners);
    }

    static void ValidateLine(List<DialogueValidationIssue> into, DialogueAsset asset, DialogueLineNode line)
    {
        string where = string.IsNullOrEmpty(line.nodeId) ? "Line" : line.nodeId;
        if (!string.IsNullOrEmpty(line.nextNodeId) && asset.FindNode(line.nextNodeId) == null)
        {
            Add(into, DialogueValidationSeverity.Error, where,
                "nextNodeId '" + line.nextNodeId + "' does not exist.",
                "Pick an existing node, or clear it to end the conversation.");
        }

        ValidateVariants(into, where, "line", line.textVariants);
    }

    static void ValidateChoice(
        List<DialogueValidationIssue> into,
        DialogueAsset asset,
        DialogueChoiceNode choice,
        HashSet<string> knownCharacters)
    {
        string where = string.IsNullOrEmpty(choice.nodeId) ? "Choice" : choice.nodeId;
        int authored = 0;
        var optionIds = new HashSet<string>();

        if (choice.options != null)
        {
            for (int i = 0; i < choice.options.Count; i++)
            {
                DialogueChoiceOption option = choice.options[i];
                if (option == null)
                {
                    Add(into, DialogueValidationSeverity.Warning, where,
                        "Option " + i + " is null and is skipped.",
                        "Remove the empty entry.");
                    continue;
                }

                authored++;
                string optionWhere = where + " / " + (string.IsNullOrEmpty(option.optionId) ? "option " + i : option.optionId);

                if (string.IsNullOrEmpty(option.optionId))
                {
                    Add(into, DialogueValidationSeverity.Warning, optionWhere,
                        "optionId is empty.",
                        "Set an id. It is also the label when text and variants are empty.");
                }
                else if (!optionIds.Add(option.optionId))
                {
                    Add(into, DialogueValidationSeverity.Error, optionWhere,
                        "optionId '" + option.optionId + "' is duplicated in this choice.",
                        "Give each option its own id.");
                }

                if (!HasLabel(option))
                {
                    Add(into, DialogueValidationSeverity.Warning, optionWhere,
                        "This option has no label.",
                        "Set text, a variant with text, or an optionId.");
                }

                if (!string.IsNullOrEmpty(option.nextNodeId) && asset.FindNode(option.nextNodeId) == null)
                {
                    Add(into, DialogueValidationSeverity.Error, optionWhere,
                        "nextNodeId '" + option.nextNodeId + "' does not exist.",
                        "Pick an existing node, or clear it to end the conversation.");
                }

                ValidateRequiredCharacter(into, optionWhere, option.requiredCharacterId, knownCharacters);
                ValidateVariants(into, optionWhere, "label", option.textVariants);
                ValidateConditions(into, optionWhere, option.availability);
                ValidateConsequences(into, optionWhere, option.consequences);
            }
        }

        if (!string.IsNullOrEmpty(choice.fallbackNodeId) && asset.FindNode(choice.fallbackNodeId) == null)
        {
            Add(into, DialogueValidationSeverity.Error, where,
                "fallbackNodeId '" + choice.fallbackNodeId + "' does not exist.",
                "Point it at a node, or clear it.");
        }

        if (authored == 0 && string.IsNullOrEmpty(choice.fallbackNodeId))
        {
            Add(into, DialogueValidationSeverity.Error, where,
                "This choice has no options and no fallback.",
                "Add an option, or set a fallback. Otherwise the session fails with empty-choice.");
        }
    }

    static void ValidateCondition(List<DialogueValidationIssue> into, DialogueAsset asset, DialogueConditionNode condition)
    {
        string where = string.IsNullOrEmpty(condition.nodeId) ? "Condition" : condition.nodeId;
        ValidateDestination(into, asset, where, "trueNodeId", condition.trueNodeId);
        ValidateDestination(into, asset, where, "falseNodeId", condition.falseNodeId);
        ValidateConditions(into, where, condition.conditions);
    }

    static void ValidateConsequences(
        List<DialogueValidationIssue> into,
        string where,
        System.Collections.Generic.List<EventAction> consequences)
    {
        if (consequences == null)
            return;

        for (int i = 0; i < consequences.Count; i++)
        {
            EventAction action = consequences[i];
            if (action == null)
            {
                Add(into, DialogueValidationSeverity.Error, where,
                    "Consequence [" + i + "] is null.",
                    "Remove the empty entry, or assign an action.");
                continue;
            }

            if (action is StartDialogueAction)
            {
                Add(into, DialogueValidationSeverity.Error, where,
                    "StartDialogueAction cannot run as a choice consequence.",
                    "Remove it. A choice must not start another dialogue session.");
            }
        }
    }

    static void ValidateDestination(
        List<DialogueValidationIssue> into,
        DialogueAsset asset,
        string where,
        string field,
        string nodeId)
    {
        if (string.IsNullOrEmpty(nodeId) || asset.FindNode(nodeId) != null)
            return;

        Add(into, DialogueValidationSeverity.Error, where,
            field + " '" + nodeId + "' does not exist.",
            "Pick an existing node, or clear it to end the conversation.");
    }

    static void ValidateRequiredCharacter(
        List<DialogueValidationIssue> into,
        string where,
        string requiredCharacterId,
        HashSet<string> knownCharacters)
    {
        if (string.IsNullOrEmpty(requiredCharacterId))
            return;

        if (string.IsNullOrWhiteSpace(requiredCharacterId))
        {
            Add(into, DialogueValidationSeverity.Warning, where,
                "requiredCharacterId is blank.",
                "Clear it so anyone can pick the option, or set a character id such as ax or jyn.");
            return;
        }

        if (DialogueActiveCharacter.IsActivePlayerAlias(requiredCharacterId))
            return;

        if (knownCharacters != null && knownCharacters.Contains(requiredCharacterId.Trim()))
            return;

        Add(into, DialogueValidationSeverity.Warning, where,
            "requiredCharacterId '" + requiredCharacterId + "' is not an active-player alias or a CharacterDefinition id.",
            "Use ax, jyn, or active_player / player / party / hero. Any other id stays hidden unless the session started as that exact id.");
    }

    static void ValidateVariants(
        List<DialogueValidationIssue> into,
        string where,
        string kind,
        List<DialogueTextVariant> variants)
    {
        if (variants == null)
            return;

        var seen = new HashSet<string>();
        for (int i = 0; i < variants.Count; i++)
        {
            DialogueTextVariant variant = variants[i];
            if (variant == null)
            {
                Add(into, DialogueValidationSeverity.Warning, where,
                    kind + " variant " + i + " is null and is skipped.",
                    "Remove the empty entry.");
                continue;
            }

            if (string.IsNullOrEmpty(variant.characterId))
            {
                Add(into, DialogueValidationSeverity.Warning, where,
                    kind + " variant " + i + " has an empty character id.",
                    "Set ax, jyn, or an active-player alias. An empty id never matches.");
                continue;
            }

            string key = VariantKey(variant.characterId);
            if (!seen.Add(key))
            {
                Add(into, DialogueValidationSeverity.Warning, where,
                    "Duplicate " + kind + " variant for '" + variant.characterId + "'.",
                    "Keep one. The first variant with text is the one that plays.");
            }

            if (string.IsNullOrEmpty(variant.text))
            {
                Add(into, DialogueValidationSeverity.Warning, where,
                    kind + " variant '" + variant.characterId + "' has empty text.",
                    "Empty text is ignored and the default text is used. Fill it or remove the variant.");
            }
        }
    }

    static void ValidateConditions(List<DialogueValidationIssue> into, string where, List<EventCondition> conditions)
    {
        if (conditions == null)
            return;

        for (int i = 0; i < conditions.Count; i++)
        {
            EventCondition condition = conditions[i];
            if (condition == null)
            {
                Add(into, DialogueValidationSeverity.Warning, where,
                    "Condition " + i + " is null and is skipped.",
                    "Remove it. Null conditions are not evaluated.");
                continue;
            }

            if (condition is FlagIsTrueCondition flagTrue && string.IsNullOrEmpty(flagTrue.flagId))
            {
                Add(into, DialogueValidationSeverity.Warning, where,
                    "Flag-is-true condition " + i + " has an empty flag id.",
                    "Set the flag id. An empty id does not check a real flag.");
            }
            else if (condition is FlagIsFalseCondition flagFalse && string.IsNullOrEmpty(flagFalse.flagId))
            {
                Add(into, DialogueValidationSeverity.Warning, where,
                    "Flag-is-false condition " + i + " has an empty flag id.",
                    "Set the flag id. An empty id does not check a real flag.");
            }
            else if (condition is EventIsCompletedCondition done && string.IsNullOrEmpty(done.eventId))
            {
                Add(into, DialogueValidationSeverity.Warning, where,
                    "Event-completed condition " + i + " has an empty event id.",
                    "Set the event id.");
            }
            else if (condition is EventIsNotCompletedCondition notDone && string.IsNullOrEmpty(notDone.eventId))
            {
                Add(into, DialogueValidationSeverity.Warning, where,
                    "Event-not-completed condition " + i + " has an empty event id.",
                    "Set the event id.");
            }
        }
    }

    static void ValidateReachable(
        List<DialogueValidationIssue> into,
        DialogueAsset asset,
        string entryId,
        Dictionary<string, int> idOwners)
    {
        var visited = new HashSet<string>();
        var pending = new Stack<string>();
        pending.Push(entryId);

        while (pending.Count > 0)
        {
            string id = pending.Pop();
            if (string.IsNullOrEmpty(id) || !visited.Add(id))
                continue;

            DialogueNode node = asset.FindNode(id);
            if (node is DialogueLineNode line)
            {
                PushIfPresent(asset, pending, line.nextNodeId);
            }
            else if (node is DialogueChoiceNode choice)
            {
                if (choice.options != null)
                {
                    for (int i = 0; i < choice.options.Count; i++)
                    {
                        DialogueChoiceOption option = choice.options[i];
                        if (option != null)
                            PushIfPresent(asset, pending, option.nextNodeId);
                    }
                }

                PushIfPresent(asset, pending, choice.fallbackNodeId);
            }
            else if (node is DialogueConditionNode condition)
            {
                PushIfPresent(asset, pending, condition.trueNodeId);
                PushIfPresent(asset, pending, condition.falseNodeId);
            }
        }

        var nodes = asset.nodes;
        if (nodes == null)
            return;

        for (int i = 0; i < nodes.Count; i++)
        {
            DialogueNode node = nodes[i];
            if (node == null || string.IsNullOrEmpty(node.nodeId))
                continue;
            if (!idOwners.TryGetValue(node.nodeId, out int first) || first != i)
                continue;
            if (visited.Contains(node.nodeId))
                continue;

            Add(into, DialogueValidationSeverity.Warning, node.nodeId,
                TypeName(node) + " '" + node.nodeId + "' is not reachable from '" + entryId + "'.",
                "Link it from a line, an option, or a fallback, or remove it.");
        }
    }

    static void PushIfPresent(DialogueAsset asset, Stack<string> pending, string id)
    {
        if (!string.IsNullOrEmpty(id) && asset.FindNode(id) != null)
            pending.Push(id);
    }

    static bool HasLabel(DialogueChoiceOption option)
    {
        if (!string.IsNullOrEmpty(option.text) || !string.IsNullOrEmpty(option.optionId))
            return true;
        if (option.textVariants == null)
            return false;
        for (int i = 0; i < option.textVariants.Count; i++)
        {
            DialogueTextVariant variant = option.textVariants[i];
            if (variant != null && !string.IsNullOrEmpty(variant.text))
                return true;
        }
        return false;
    }

    static string VariantKey(string characterId)
    {
        if (DialogueActiveCharacter.IsActivePlayerAlias(characterId))
            return "active_player";
        return characterId.Trim().ToLowerInvariant();
    }

    static HashSet<string> LoadCharacterIds()
    {
        var ids = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        string[] guids = AssetDatabase.FindAssets("t:CharacterDefinition");
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            var definition = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(path);
            if (definition != null && !string.IsNullOrEmpty(definition.id))
                ids.Add(definition.id.Trim());
        }
        return ids;
    }

    static string TypeName(DialogueNode node)
    {
        if (node is DialogueLineNode)
            return "Line";
        if (node is DialogueChoiceNode)
            return "Choice";
        if (node is DialogueConditionNode)
            return "Condition";
        if (node is DialogueEndNode)
            return "End";
        return node.GetType().Name;
    }

    static void Add(
        List<DialogueValidationIssue> into,
        DialogueValidationSeverity severity,
        string location,
        string problem,
        string correction)
    {
        into.Add(new DialogueValidationIssue
        {
            Severity = severity,
            Location = location,
            Problem = problem,
            Correction = correction,
        });
    }

    [MenuItem("Tools/ARPG/Events/Run Validation Self-Check (Phase B Step 8)", false, 214)]
    public static void RunSelfCheck()
    {
        var failures = new List<string>();
        CheckValid(failures);
        CheckMissingEntry(failures);
        CheckDuplicateNode(failures);
        CheckBrokenLine(failures);
        CheckBrokenChoice(failures);
        CheckEmptyChoice(failures);
        CheckDuplicateOption(failures);
        CheckVariants(failures);
        CheckTerminals(failures);
        CheckConditionDestination(failures);
        CheckRejectedConsequence(failures);
        CheckExistingAssetUntouched(failures);

        if (failures.Count == 0)
        {
            Debug.Log("[Dialogue] Step 8 validation self-check: 12 passed.");
            EditorUtility.DisplayDialog(
                "Dialogue validation",
                "Step 8 self-check: 12 passed.\n\nSelect a Dialogue asset to see its validation box in the Inspector.",
                "OK");
        }
        else
        {
            string message = string.Join("\n", failures);
            Debug.LogWarning("[Dialogue] Step 8 validation self-check failed: " + string.Join(" | ", failures));
            EditorUtility.DisplayDialog("Dialogue validation", "Self-check failed:\n\n" + message, "OK");
        }
    }

    static void CheckValid(List<string> failures)
    {
        var asset = Asset(
            "ok",
            "a",
            Line("a", "b"),
            End("b"));
        Expect(failures, "valid", asset, DialogueValidationSeverity.Error, 0);
        Expect(failures, "valid-warnings", asset, DialogueValidationSeverity.Warning, 0);
        Object.DestroyImmediate(asset);
    }

    static void CheckMissingEntry(List<string> failures)
    {
        var asset = Asset("ok", "missing", Line("a", ""));
        if (!HasProblem(asset, DialogueValidationSeverity.Error, "does not match"))
            failures.Add("missing entry was not reported");
        Object.DestroyImmediate(asset);
    }

    static void CheckDuplicateNode(List<string> failures)
    {
        var asset = Asset("ok", "a", Line("a", ""), Line("a", ""));
        if (!HasProblem(asset, DialogueValidationSeverity.Error, "duplicated"))
            failures.Add("duplicate node id was not reported");
        Object.DestroyImmediate(asset);
    }

    static void CheckBrokenLine(List<string> failures)
    {
        var asset = Asset("ok", "a", Line("a", "gone"));
        if (!HasProblem(asset, DialogueValidationSeverity.Error, "nextNodeId 'gone'"))
            failures.Add("broken line destination was not reported");
        Object.DestroyImmediate(asset);
    }

    static void CheckBrokenChoice(List<string> failures)
    {
        var choice = Choice("c", "", Option("yes", "Yes", "gone"));
        var asset = Asset("ok", "c", choice);
        if (!HasProblem(asset, DialogueValidationSeverity.Error, "nextNodeId 'gone'"))
            failures.Add("broken choice destination was not reported");
        Object.DestroyImmediate(asset);
    }

    static void CheckEmptyChoice(List<string> failures)
    {
        var asset = Asset("ok", "c", Choice("c", "", null));
        if (!HasProblem(asset, DialogueValidationSeverity.Error, "no options and no fallback"))
            failures.Add("empty choice was not reported");
        Object.DestroyImmediate(asset);
    }

    static void CheckDuplicateOption(List<string> failures)
    {
        var choice = Choice("c", "", Option("same", "One", ""), Option("same", "Two", ""));
        var asset = Asset("ok", "c", choice);
        if (!HasProblem(asset, DialogueValidationSeverity.Error, "optionId 'same' is duplicated"))
            failures.Add("duplicate option id was not reported");
        Object.DestroyImmediate(asset);
    }

    static void CheckVariants(List<string> failures)
    {
        var line = Line("a", "");
        line.textVariants = new List<DialogueTextVariant>
        {
            new DialogueTextVariant { characterId = "", text = "Hi" },
            new DialogueTextVariant { characterId = "ax", text = "Ax" },
            new DialogueTextVariant { characterId = "Ax", text = "Again" },
        };
        var asset = Asset("ok", "a", line);
        if (!HasProblem(asset, DialogueValidationSeverity.Warning, "empty character id"))
            failures.Add("empty variant character id was not reported");
        if (!HasProblem(asset, DialogueValidationSeverity.Warning, "Duplicate"))
            failures.Add("duplicate variant character id was not reported");
        Object.DestroyImmediate(asset);
    }

    static void CheckTerminals(List<string> failures)
    {
        var lineOnly = Asset("ok", "a", Line("a", ""));
        if (Count(lineOnly, DialogueValidationSeverity.Error) != 0)
            failures.Add("empty line destination was flagged");
        Object.DestroyImmediate(lineOnly);

        var choice = Choice("c", "", Option("leave", "Leave", ""));
        var branched = Asset("ok", "a", Line("a", "c"), choice);
        if (Count(branched, DialogueValidationSeverity.Error) != 0)
            failures.Add("empty option destination was flagged");
        Object.DestroyImmediate(branched);
    }

    static void CheckConditionDestination(List<string> failures)
    {
        var asset = Asset(
            "cond",
            "gate",
            new DialogueConditionNode
            {
                nodeId = "gate",
                trueNodeId = "yes",
                falseNodeId = "missing",
            },
            Line("yes", ""));
        if (!HasProblem(asset, DialogueValidationSeverity.Error, "falseNodeId 'missing'"))
            failures.Add("condition: missing false destination was not an error");
        if (HasProblem(asset, DialogueValidationSeverity.Error, "trueNodeId 'yes'"))
            failures.Add("condition: a real true destination was reported missing");
        Object.DestroyImmediate(asset);
    }

    static void CheckRejectedConsequence(List<string> failures)
    {
        var option = Option("b", "Set", "yes");
        option.consequences = new System.Collections.Generic.List<EventAction>
        {
            null,
            new StartDialogueAction(),
            new SetFlagAction { flagId = "sample" },
        };
        var asset = Asset("ok", "c", Choice("c", "", option), Line("yes", ""));
        if (!HasProblem(asset, DialogueValidationSeverity.Error, "Consequence [0] is null"))
            failures.Add("null consequence was not reported");
        if (!HasProblem(asset, DialogueValidationSeverity.Error, "StartDialogueAction cannot run"))
            failures.Add("nested StartDialogueAction was not rejected");
        if (HasProblem(asset, DialogueValidationSeverity.Error, "SetFlag"))
            failures.Add("SetFlag consequence was rejected");
        Object.DestroyImmediate(asset);
    }

    static int Count(DialogueAsset asset, DialogueValidationSeverity severity)
    {
        var issues = new List<DialogueValidationIssue>();
        Collect(asset, issues);
        int found = 0;
        for (int i = 0; i < issues.Count; i++)
        {
            if (issues[i].Severity == severity)
                found++;
        }
        return found;
    }

    static void CheckExistingAssetUntouched(List<string> failures)
    {
        const string path = "Assets/Game/Dialogue/TestLab/Lab_Place_Left.asset";
        var asset = AssetDatabase.LoadAssetAtPath<DialogueAsset>(path);
        if (asset == null)
        {
            failures.Add("Lab_Place_Left was not found");
            return;
        }

        bool dirtyBefore = EditorUtility.IsDirty(asset);
        string id = asset.dialogueId;
        int count = asset.nodes != null ? asset.nodes.Count : -1;
        string entry = asset.entryNodeId;
        var issues = new List<DialogueValidationIssue>();
        Collect(asset, issues);
        if (asset.dialogueId != id || asset.entryNodeId != entry ||
            (asset.nodes != null ? asset.nodes.Count : -1) != count)
            failures.Add("validation rewrote Lab_Place_Left");
        if (EditorUtility.IsDirty(asset) != dirtyBefore)
            failures.Add("validation changed the dirty state of Lab_Place_Left");
    }

    static void Expect(List<string> failures, string name, DialogueAsset asset, DialogueValidationSeverity severity, int count)
    {
        int found = 0;
        var issues = new List<DialogueValidationIssue>();
        Collect(asset, issues);
        for (int i = 0; i < issues.Count; i++)
        {
            if (issues[i].Severity == severity)
                found++;
        }

        if (found != count)
            failures.Add(name + " expected " + count + " " + severity + " but found " + found);
    }

    static bool HasProblem(DialogueAsset asset, DialogueValidationSeverity severity, string fragment)
    {
        var issues = new List<DialogueValidationIssue>();
        Collect(asset, issues);
        for (int i = 0; i < issues.Count; i++)
        {
            if (issues[i].Severity == severity &&
                issues[i].Problem != null &&
                issues[i].Problem.IndexOf(fragment, System.StringComparison.Ordinal) >= 0)
                return true;
        }
        return false;
    }

    static DialogueAsset Asset(string dialogueId, string entry, params DialogueNode[] nodes)
    {
        var asset = ScriptableObject.CreateInstance<DialogueAsset>();
        asset.dialogueId = dialogueId;
        asset.entryNodeId = entry;
        asset.nodes = new List<DialogueNode>(nodes);
        return asset;
    }

    static DialogueLineNode Line(string id, string next)
    {
        return new DialogueLineNode
        {
            nodeId = id,
            speakerId = "villager_a",
            text = "Hello",
            nextNodeId = next,
        };
    }

    static DialogueEndNode End(string id)
    {
        return new DialogueEndNode { nodeId = id };
    }

    static DialogueChoiceNode Choice(string id, string fallback, params DialogueChoiceOption[] options)
    {
        return new DialogueChoiceNode
        {
            nodeId = id,
            fallbackNodeId = fallback,
            options = options == null
                ? new List<DialogueChoiceOption>()
                : new List<DialogueChoiceOption>(options),
        };
    }

    static DialogueChoiceOption Option(string id, string text, string next)
    {
        return new DialogueChoiceOption
        {
            optionId = id,
            text = text,
            nextNodeId = next,
        };
    }
}
