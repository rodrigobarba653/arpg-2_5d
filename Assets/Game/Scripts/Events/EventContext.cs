using System.Threading;
using UnityEngine;

/// <summary>Shared runtime state passed to every action in a sequence.</summary>
public sealed class EventContext
{
    public EventRunner Runner { get; }
    public CancellationToken CancellationToken { get; }
    public bool IsCancelled => CancellationToken.IsCancellationRequested;

    /// <summary>
    /// The current action ended the sequence without a successful run.
    /// Prior actions are kept. The runner does not mark Once or WorldState completed.
    /// </summary>
    public bool StopRequested { get; private set; }

    public string StopReason { get; private set; }

    public EventContext(EventRunner runner, CancellationToken cancellationToken)
    {
        Runner = runner;
        CancellationToken = cancellationToken;
    }

    public void RequestStop(string reason)
    {
        if (StopRequested)
            return;
        StopRequested = true;
        StopReason = string.IsNullOrEmpty(reason) ? "stopped" : reason;
    }

    public void Log(string message)
    {
        if (Runner != null && Runner.debugLog)
            Debug.Log($"[Event:{Runner.name}] {message}", Runner);
    }
}
