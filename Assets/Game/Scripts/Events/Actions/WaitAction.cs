using System.Collections;
using UnityEngine;

[System.Serializable]
public class WaitAction : EventAction
{
    [Min(0f)]
    public float seconds = 1f;

    public override string DisplayName => $"Wait ({seconds:0.##}s)";

    public override IEnumerator Execute(EventContext ctx)
    {
        float remaining = Mathf.Max(0f, seconds);
        while (remaining > 0f)
        {
            if (ctx.IsCancelled)
                yield break;

            remaining -= Time.deltaTime;
            yield return null;
        }
    }
}
