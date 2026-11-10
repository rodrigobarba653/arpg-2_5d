using System.Threading;
using UnityEngine;

/// <summary>Shared runtime state passed to every action in a sequence.</summary>
public sealed class EventContext
{
    public EventRunner Runner { get; }
    public CancellationToken CancellationToken { get; }
    public bool IsCancelled => CancellationToken.IsCancellationRequested;

    public EventContext(EventRunner runner, CancellationToken cancellationToken)
    {
        Runner = runner;
        CancellationToken = cancellationToken;
    }

    public void Log(string message)
    {
        if (Runner != null && Runner.debugLog)
            Debug.Log($"[Event:{Runner.name}] {message}", Runner);
    }
}
