using UnityEngine;

/// <summary>On-screen lock/scheduler/world-state status for EventSystem_TestLab.</summary>
public class EventSystemDebugHud : MonoBehaviour
{
    public bool show = true;

    GUIStyle style;

    void OnGUI()
    {
        if (!show)
            return;

        if (style == null)
        {
            style = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.UpperLeft,
                fontSize = 13,
                richText = true,
            };
        }

        string text =
            "<b>Event System Debug</b>\n" +
            $"timeScale={Time.timeScale:0.##}\n" +
            $"GamePause={GamePause.IsPaused}\n" +
            $"GameplayLock: {GameplayLock.DebugSummary()}\n" +
            $"Scheduler: {EventScheduler.DebugSummary()}\n" +
            $"WorldState: {WorldState.DebugSummary()}";

        GUI.Box(new Rect(12, 12, 460, 120), text, style);
    }
}
