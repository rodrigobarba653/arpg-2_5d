using System;
using System.Collections.Generic;

/// <summary>
/// Single catalog of EventAction types + categories for Inspector Add menus.
/// Context filters keep dialogue consequences free of EventRunner-only actions.
/// </summary>
public static class EventActionRegistry
{
    public enum AuthorContext
    {
        EventRunner = 0,
        ParallelChild = 1,
        DialogueConsequence = 2,
    }

    public readonly struct Entry
    {
        public readonly Type Type;
        public readonly string Category;
        public readonly string MenuName;
        public readonly bool EventRunnerOnly;
        public readonly bool ParallelForbidden;

        public Entry(Type type, string category, string menuName, bool eventRunnerOnly, bool parallelForbidden)
        {
            Type = type;
            Category = category;
            MenuName = menuName;
            EventRunnerOnly = eventRunnerOnly;
            ParallelForbidden = parallelForbidden;
        }
    }

    static readonly Entry[] All =
    {
        // Flow
        new Entry(typeof(WaitAction), "Flow", "Wait", false, false),
        new Entry(typeof(DebugLogAction), "Flow", "Debug Log", false, false),
        new Entry(typeof(UnityEventAction), "Flow", "Unity Event", false, false),
        new Entry(typeof(PlayAudioAction), "Audio", "Play Audio", false, false),
        new Entry(typeof(ParallelActions), "Flow", "Parallel", true, true),
        // DelayedStart is created by Parallel delay UX — not listed in Add menus.

        // Camera (EventRunner / Parallel only)
        new Entry(typeof(CameraFocusAction), "Camera", "Camera Focus", true, false),
        new Entry(typeof(CameraRestoreAction), "Camera", "Camera Restore", true, false),
        new Entry(typeof(CameraShotAction), "Camera", "Camera Shot", true, false),
        new Entry(typeof(CameraMoveAction), "Camera", "Camera Move", true, false),
        new Entry(typeof(CameraShakeAction), "Camera", "Camera Shake", true, false),
        new Entry(typeof(GameplayFollowOffsetAction), "Camera", "Gameplay Follow Offset", true, false),

        // Characters
        new Entry(typeof(MoveCharacterAction), "Characters", "Move Character", false, false),
        new Entry(typeof(FaceCharacterAction), "Characters", "Face Character", false, false),
        new Entry(typeof(PlayCharacterAnimationAction), "Characters", "Play Animation", false, false),

        // Dialogue
        new Entry(typeof(StartDialogueAction), "Dialogue", "Start Dialogue", true, false),

        // Timeline (EventRunner only — needs runner cancel + camera lease policy)
        // Not a Parallel child: lease Abort would fight CameraShot / other pose writers.
        new Entry(typeof(PlayTimelineAction), "Timeline", "Play Timeline", true, true),

        // World / doors / flags
        new Entry(typeof(SetFlagAction), "World", "Set Flag", false, false),
        new Entry(typeof(ClearFlagAction), "World", "Clear Flag", false, false),
        new Entry(typeof(MarkEventCompletedAction), "World", "Mark Event Completed", false, false),
        new Entry(typeof(OpenDoorAction), "World", "Open Door", false, false),
        new Entry(typeof(CloseDoorAction), "World", "Close Door", false, false),
        new Entry(typeof(SetDoorStateAction), "World", "Set Door State", false, false),
        new Entry(typeof(UnlockDoorAction), "World", "Unlock Door", false, false),
        new Entry(typeof(MoveObjectAction), "World", "Move Object", false, false),

        // Rewards
        new Entry(typeof(GrantRewardsAction), "Rewards", "Grant Rewards", false, false),
        new Entry(typeof(ClaimChestAction), "Rewards", "Claim Chest", true, false),
    };

    public static IEnumerable<Entry> ForContext(AuthorContext context)
    {
        for (int i = 0; i < All.Length; i++)
        {
            var e = All[i];
            if (context == AuthorContext.DialogueConsequence && e.EventRunnerOnly)
                continue;
            if (context == AuthorContext.ParallelChild && e.ParallelForbidden)
                continue;
            yield return e;
        }
    }

    public static Type[] TypesForContext(AuthorContext context)
    {
        var list = new List<Type>(All.Length);
        foreach (var e in ForContext(context))
            list.Add(e.Type);
        return list.ToArray();
    }
}
