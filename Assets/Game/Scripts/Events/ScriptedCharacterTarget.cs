using System;
using UnityEngine;

/// <summary>
/// How an EventAction picks the body it steers. Dormant party members are not
/// supported — use the active player or an explicit scene object.
/// </summary>
public enum ScriptedCharacterSource
{
    ActivePlayer = 0,
    Explicit = 1,
}

[Serializable]
public class ScriptedCharacterTarget
{
    public ScriptedCharacterSource source = ScriptedCharacterSource.ActivePlayer;

    [Tooltip("Used when Source is Explicit. PlayerHealth, PlayerMotor, ScriptedNpcMotor, or any Transform.")]
    public Transform explicitTransform;

    public bool TryResolve(out Transform body, out PlayerMotor playerMotor, out ScriptedNpcMotor npcMotor, out string failure)
    {
        body = null;
        playerMotor = null;
        npcMotor = null;
        failure = null;

        if (source == ScriptedCharacterSource.ActivePlayer)
        {
            var active = Party.Active;
            if (active == null)
            {
                failure = "no active player";
                return false;
            }

            body = active.transform;
            playerMotor = active.GetComponent<PlayerMotor>();
            if (playerMotor == null)
            {
                failure = "active player has no PlayerMotor";
                return false;
            }

            if (!playerMotor.isActiveAndEnabled)
            {
                failure = "active PlayerMotor is disabled";
                return false;
            }

            return true;
        }

        if (explicitTransform == null)
        {
            failure = "explicit character Transform is missing";
            return false;
        }

        body = explicitTransform;
        playerMotor = explicitTransform.GetComponent<PlayerMotor>();
        if (playerMotor == null)
            playerMotor = explicitTransform.GetComponentInParent<PlayerMotor>();
        npcMotor = explicitTransform.GetComponent<ScriptedNpcMotor>();
        if (npcMotor == null)
            npcMotor = explicitTransform.GetComponentInParent<ScriptedNpcMotor>();

        if (playerMotor != null)
        {
            if (!playerMotor.isActiveAndEnabled)
            {
                failure = "explicit PlayerMotor is disabled (dormant party members are not supported)";
                return false;
            }

            body = playerMotor.transform;
            npcMotor = null;
            return true;
        }

        if (npcMotor != null)
        {
            if (!npcMotor.isActiveAndEnabled)
            {
                failure = "ScriptedNpcMotor is disabled";
                return false;
            }

            body = npcMotor.transform;
            return true;
        }

        failure = "explicit target has neither PlayerMotor nor ScriptedNpcMotor";
        return false;
    }
}
