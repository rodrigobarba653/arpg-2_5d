using System.Collections;
using UnityEngine;
using UnityEngine.Events;

[System.Serializable]
public class UnityEventAction : EventAction
{
    public UnityEvent onExecute = new UnityEvent();

    public override string DisplayName => "UnityEvent";

    public override IEnumerator Execute(EventContext ctx)
    {
        onExecute?.Invoke();
        yield break;
    }
}
