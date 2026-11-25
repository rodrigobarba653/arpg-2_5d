using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Reachable dialogue structure for one session. Layout only.
/// Does not detect automatic playback cycles and does not filter choice options.
/// </summary>
public static class DialogueGraph
{
    public const int MaxSimplePaths = 1024;

    public sealed class Analysis
    {
        public bool exceededPathLimit;
        public readonly List<string> reachable = new List<string>(8);
        public readonly List<string> core = new List<string>(4);
        public readonly List<string> branch = new List<string>(8);
        public readonly List<int> branchRegion = new List<int>(8);
        public readonly List<string[]> paths = new List<string[]>(8);
    }

    public static Analysis Analyze(DialogueAsset asset, string sessionCharacterId)
    {
        var analysis = new Analysis();
        if (asset == null)
            return analysis;

        var reachableSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var trunk = new List<string>(4);
        CollectReachable(asset, sessionCharacterId, analysis.reachable, reachableSet);
        CollectTrunk(asset, sessionCharacterId, trunk);

        var walker = new Walker(asset, sessionCharacterId, analysis);
        var entry = asset.GetEntry();
        if (entry != null && !string.IsNullOrEmpty(entry.nodeId))
            walker.Visit(entry.nodeId);

        if (analysis.exceededPathLimit)
        {
            analysis.paths.Clear();
            analysis.core.Clear();
            CopyInReachableOrder(trunk, analysis.reachable, analysis.core);
            FillBranch(analysis.reachable, analysis.core, analysis.branch);
            for (int i = 0; i < analysis.branch.Count; i++)
                analysis.branchRegion.Add(i);
            return analysis;
        }

        List<string> coreSource = IntersectPaths(analysis.paths);
        if (coreSource.Count == 0)
        {
            if (trunk.Count > 0)
                coreSource = trunk;
            else
                coreSource = LongestPath(analysis.paths);
        }

        CopyInReachableOrder(coreSource, analysis.reachable, analysis.core);
        FillBranch(analysis.reachable, analysis.core, analysis.branch);
        AssignRegions(analysis);
        return analysis;
    }

    static void CollectReachable(
        DialogueAsset asset,
        string sessionCharacterId,
        List<string> order,
        HashSet<string> seenSpeakers)
    {
        var queue = new Queue<string>();
        var seenNodes = new HashSet<string>(StringComparer.Ordinal);
        string entryId = EntryId(asset);
        if (!string.IsNullOrEmpty(entryId))
            queue.Enqueue(entryId);

        while (queue.Count > 0)
        {
            string id = queue.Dequeue();
            if (string.IsNullOrEmpty(id) || !seenNodes.Add(id))
                continue;

            var node = asset.FindNode(id);
            if (node == null)
                continue;

            if (node is DialogueLineNode line)
            {
                // Screen banners are not L/R conversation speakers (even if speaker is Player/NPC).
                if (line.chatboxAim != DialogueChatboxAim.Screen)
                    AddSpeaker(order, seenSpeakers, line.speakerId, sessionCharacterId);
                if (!string.IsNullOrEmpty(line.nextNodeId))
                    queue.Enqueue(line.nextNodeId);
            }
            else if (node is DialogueChoiceNode choice)
            {
                EnqueueChoice(queue, choice);
            }
            else if (node is DialogueConditionNode condition)
            {
                if (!string.IsNullOrEmpty(condition.trueNodeId))
                    queue.Enqueue(condition.trueNodeId);
                if (!string.IsNullOrEmpty(condition.falseNodeId))
                    queue.Enqueue(condition.falseNodeId);
            }
        }
    }

    static void CollectTrunk(DialogueAsset asset, string sessionCharacterId, List<string> trunk)
    {
        var seenSpeakers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenNodes = new HashSet<string>(StringComparer.Ordinal);
        string id = EntryId(asset);
        while (!string.IsNullOrEmpty(id) && seenNodes.Add(id))
        {
            var node = asset.FindNode(id);
            if (!(node is DialogueLineNode line))
                break;

            if (line.chatboxAim != DialogueChatboxAim.Screen)
                AddSpeaker(trunk, seenSpeakers, line.speakerId, sessionCharacterId);
            if (string.IsNullOrEmpty(line.nextNodeId))
                break;

            var next = asset.FindNode(line.nextNodeId);
            if (next is DialogueChoiceNode)
                break;
            id = line.nextNodeId;
        }
    }

    static void EnqueueChoice(Queue<string> queue, DialogueChoiceNode choice)
    {
        if (choice.options != null)
        {
            for (int i = 0; i < choice.options.Count; i++)
            {
                var option = choice.options[i];
                if (option != null && !string.IsNullOrEmpty(option.nextNodeId))
                    queue.Enqueue(option.nextNodeId);
            }
        }

        if (!string.IsNullOrEmpty(choice.fallbackNodeId))
            queue.Enqueue(choice.fallbackNodeId);
    }

    static string EntryId(DialogueAsset asset)
    {
        if (!string.IsNullOrEmpty(asset.entryNodeId))
            return asset.entryNodeId;
        if (asset.nodes != null && asset.nodes.Count > 0 && asset.nodes[0] != null)
            return asset.nodes[0].nodeId;
        return "";
    }

    static void AddSpeaker(List<string> order, HashSet<string> seen, string authoredId, string sessionCharacterId)
    {
        if (string.IsNullOrEmpty(authoredId))
            return;
        if (DialogueActiveCharacter.IsScreenAlias(authoredId))
            return; // Screen banners are not conversation L/R speakers.
        string resolved = DialogueActiveCharacter.ResolveSpeakerId(authoredId, sessionCharacterId);
        if (string.IsNullOrEmpty(resolved))
            resolved = "_default";
        if (DialogueActiveCharacter.IsScreenAlias(resolved))
            return;
        if (seen.Add(resolved))
            order.Add(resolved);
    }

    static List<string> IntersectPaths(List<string[]> paths)
    {
        var result = new List<string>();
        if (paths == null || paths.Count == 0)
            return result;

        var acc = new HashSet<string>(paths[0], StringComparer.OrdinalIgnoreCase);
        for (int i = 1; i < paths.Count; i++)
        {
            var next = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var path = paths[i];
            for (int s = 0; s < path.Length; s++)
            {
                if (acc.Contains(path[s]))
                    next.Add(path[s]);
            }
            acc = next;
            if (acc.Count == 0)
                break;
        }

        foreach (var id in acc)
            result.Add(id);
        return result;
    }

    static List<string> LongestPath(List<string[]> paths)
    {
        var best = new List<string>();
        if (paths == null)
            return best;
        for (int i = 0; i < paths.Count; i++)
        {
            if (paths[i].Length > best.Count)
                best = new List<string>(paths[i]);
        }
        return best;
    }

    static void CopyInReachableOrder(List<string> source, List<string> reachable, List<string> into)
    {
        var wanted = new HashSet<string>(source, StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < reachable.Count; i++)
        {
            if (wanted.Contains(reachable[i]))
                into.Add(reachable[i]);
        }
    }

    static void FillBranch(List<string> reachable, List<string> core, List<string> branch)
    {
        var coreSet = new HashSet<string>(core, StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < reachable.Count; i++)
        {
            if (!coreSet.Contains(reachable[i]))
                branch.Add(reachable[i]);
        }
    }

    static void AssignRegions(Analysis analysis)
    {
        analysis.branchRegion.Clear();
        for (int i = 0; i < analysis.branch.Count; i++)
        {
            int region = 0;
            while (RegionTaken(analysis, i, region))
                region++;
            analysis.branchRegion.Add(region);
        }
    }

    static bool RegionTaken(Analysis analysis, int branchIndex, int region)
    {
        string speaker = analysis.branch[branchIndex];
        for (int earlier = 0; earlier < branchIndex; earlier++)
        {
            if (analysis.branchRegion[earlier] != region)
                continue;
            if (SharePath(analysis.paths, speaker, analysis.branch[earlier]))
                return true;
        }
        return false;
    }

    static bool SharePath(List<string[]> paths, string a, string b)
    {
        for (int p = 0; p < paths.Count; p++)
        {
            bool hasA = false;
            bool hasB = false;
            var path = paths[p];
            for (int i = 0; i < path.Length; i++)
            {
                if (string.Equals(path[i], a, StringComparison.OrdinalIgnoreCase))
                    hasA = true;
                if (string.Equals(path[i], b, StringComparison.OrdinalIgnoreCase))
                    hasB = true;
            }
            if (hasA && hasB)
                return true;
        }
        return false;
    }

    sealed class Walker
    {
        readonly DialogueAsset asset;
        readonly string sessionCharacterId;
        readonly Analysis analysis;
        readonly HashSet<string> pathNodes = new HashSet<string>(StringComparer.Ordinal);
        readonly Dictionary<string, int> speakerCounts =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        int pathCount;

        public Walker(DialogueAsset asset, string sessionCharacterId, Analysis analysis)
        {
            this.asset = asset;
            this.sessionCharacterId = sessionCharacterId;
            this.analysis = analysis;
        }

        public void Visit(string nodeId)
        {
            if (analysis.exceededPathLimit)
                return;

            if (string.IsNullOrEmpty(nodeId) || pathNodes.Contains(nodeId))
            {
                Commit();
                return;
            }

            var node = asset.FindNode(nodeId);
            if (node == null)
            {
                Commit();
                return;
            }

            pathNodes.Add(nodeId);
            if (node is DialogueLineNode line)
                VisitLine(line);
            else if (node is DialogueEndNode)
                Commit();
            else if (node is DialogueChoiceNode choice)
                VisitChoice(choice);
            else if (node is DialogueConditionNode condition)
                VisitCondition(condition);
            else
                Commit();
            pathNodes.Remove(nodeId);
        }

        void VisitLine(DialogueLineNode line)
        {
            string pushed = Push(line.speakerId);
            if (string.IsNullOrEmpty(line.nextNodeId))
                Commit();
            else
                Visit(line.nextNodeId);
            Pop(pushed);
        }

        void VisitChoice(DialogueChoiceNode choice)
        {
            bool any = false;
            if (choice.options != null)
            {
                for (int i = 0; i < choice.options.Count; i++)
                {
                    var option = choice.options[i];
                    if (option == null)
                        continue;
                    any = true;
                    if (string.IsNullOrEmpty(option.nextNodeId))
                        Commit();
                    else
                        Visit(option.nextNodeId);
                    if (analysis.exceededPathLimit)
                        return;
                }
            }

            if (!string.IsNullOrEmpty(choice.fallbackNodeId))
            {
                any = true;
                Visit(choice.fallbackNodeId);
            }

            if (!any)
                Commit();
        }

        void VisitCondition(DialogueConditionNode condition)
        {
            VisitDestination(condition.trueNodeId);
            if (analysis.exceededPathLimit)
                return;
            VisitDestination(condition.falseNodeId);
        }

        void VisitDestination(string nextId)
        {
            if (string.IsNullOrEmpty(nextId))
                Commit();
            else
                Visit(nextId);
        }

        void Commit()
        {
            pathCount++;
            if (pathCount > MaxSimplePaths)
            {
                analysis.exceededPathLimit = true;
                return;
            }

            var ids = new string[speakerCounts.Count];
            int n = 0;
            foreach (var kvp in speakerCounts)
                ids[n++] = kvp.Key;
            analysis.paths.Add(ids);
        }

        string Push(string authoredId)
        {
            if (string.IsNullOrEmpty(authoredId))
                return null;
            string resolved = DialogueActiveCharacter.ResolveSpeakerId(authoredId, sessionCharacterId);
            if (string.IsNullOrEmpty(resolved))
                resolved = "_default";
            speakerCounts.TryGetValue(resolved, out int count);
            speakerCounts[resolved] = count + 1;
            return resolved;
        }

        void Pop(string resolved)
        {
            if (string.IsNullOrEmpty(resolved))
                return;
            if (!speakerCounts.TryGetValue(resolved, out int count))
                return;
            if (count <= 1)
                speakerCounts.Remove(resolved);
            else
                speakerCounts[resolved] = count - 1;
        }
    }
}
