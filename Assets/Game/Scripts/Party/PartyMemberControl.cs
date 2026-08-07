using UnityEngine;

/// <summary>
/// Toggles "controlled" state on a party member: when controlled, renderer is
/// visible, CharacterController + motor + combat are enabled, and the player
/// can be steered. When NOT controlled, these are disabled — the GameObject
/// still exists (and PlayerHealth is still registered with Party.Members), but
/// it's invisible and doesn't process input or physics.
///
/// Auto-discovers the components on Awake. Drop on each party member's root.
/// PartyRoot calls SetControlled(true/false) on swap.
/// </summary>
[DisallowMultipleComponent]
public class PartyMemberControl : MonoBehaviour
{
    [Tooltip("Initial state. Usually set externally by PartyRoot — this just " +
             "defines what the character looks like in the editor / first frame.")]
    public bool isControlled = true;

    Renderer[] renderers;
    PlayerMotor motor;
    PlayerCombatController combat;
    PlayerJump jump;
    PlayerSwimming swimming;
    PlayerEquipment equipment;
    CharacterController cc;
    UnityEngine.InputSystem.PlayerInput playerInput;

    void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>(includeInactive: true);
        motor = GetComponent<PlayerMotor>();
        combat = GetComponent<PlayerCombatController>();
        jump = GetComponent<PlayerJump>();
        swimming = GetComponent<PlayerSwimming>();
        equipment = GetComponent<PlayerEquipment>();
        cc = GetComponent<CharacterController>();
        playerInput = GetComponent<UnityEngine.InputSystem.PlayerInput>();

        ApplyControl();
    }

    public void SetControlled(bool value)
    {
        if (isControlled == value) return;
        isControlled = value;
        ApplyControl();
    }

    void ApplyControl()
    {
        // CharacterController off when not controlled so the puppet doesn't
        // collide / fall on its own. Re-enabled on the next active swap.
        if (cc != null) cc.enabled = isControlled;

        // Disable input-processing scripts on the puppet.
        if (motor != null)     motor.enabled    = isControlled;
        if (combat != null)    combat.enabled   = isControlled;
        if (jump != null)      jump.enabled     = isControlled;
        if (swimming != null)  swimming.enabled = isControlled;

        // Disable PlayerEquipment too so the dormant member doesn't react to
        // inventory events (auto-equip a pickup, swap its animator controller
        // mid-game, etc.). Only the active member tracks equipment changes.
        if (equipment != null) equipment.enabled = isControlled;

        // Toggle PlayerInput too — when re-enabled it discards any in-flight
        // input state so a button held during the previous character's actions
        // doesn't carry over (e.g. pickup-interact triggering a jump on swap).
        if (playerInput != null)
        {
            // Disable first so the next enable cycle starts clean.
            playerInput.enabled = false;
            playerInput.enabled = isControlled;
        }

        // Hide all renderers (sprite + shadow + child sprites etc.).
        if (renderers != null)
        {
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i] != null) renderers[i].enabled = isControlled;
        }
    }
}
