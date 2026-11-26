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
}
