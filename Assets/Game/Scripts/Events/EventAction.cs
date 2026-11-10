using System.Collections;
using UnityEngine;

/// <summary>
/// Base type for SerializeReference event actions.
/// Concrete actions live under Assets/Game/Scripts/Events/Actions/.
/// </summary>
[System.Serializable]
public abstract class EventAction
{
    public abstract string DisplayName { get; }

    public abstract IEnumerator Execute(EventContext ctx);

    /// <summary>Called when the runner cancels mid-sequence. Default is no-op.</summary>
    public virtual void Cancel(EventContext ctx) { }
}
