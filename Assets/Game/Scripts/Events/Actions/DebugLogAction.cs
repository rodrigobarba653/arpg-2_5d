using System.Collections;
using UnityEngine;

[System.Serializable]
public class DebugLogAction : EventAction
{
    [TextArea(1, 3)]
    public string message = "Event step";

    public override string DisplayName =>
        string.IsNullOrEmpty(message) ? "Log" : $"Log: {Trim(message, 40)}";

    public override IEnumerator Execute(EventContext ctx)
    {
        // One console line. Runner also prints "[i] Log: …" when debugLog is on;
        // keep this so the action still works if debugLog is disabled on the runner.
        Debug.Log($"[EventLog] {message}", ctx.Runner);
        yield break;
    }

    static string Trim(string value, int max)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= max)
            return value;
        return value.Substring(0, max - 1) + "…";
    }
}
