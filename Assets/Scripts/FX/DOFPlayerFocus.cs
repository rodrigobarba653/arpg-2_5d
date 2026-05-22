using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Keeps the URP Depth of Field focus locked on a target (the player) so the
/// character stays sharp while everything closer and farther blurs.
///
/// Setup:
///   1. Add this to the GameObject that holds the Volume (with a DOF override).
///   2. Assign Target to the player and Cam to the gameplay camera.
///   3. In the Volume's DOF override, choose Mode = Bokeh.
/// </summary>
public class DOFPlayerFocus : MonoBehaviour
{
    [Header("Refs")]
    [Tooltip("The Volume containing the Depth Of Field override.")]
    public Volume volume;

    [Tooltip("Transform to keep in focus (player root).")]
    public Transform target;

    [Tooltip("Camera used to measure distance to the target. Defaults to Camera.main.")]
    public Camera cam;

    [Header("Tuning")]
    [Tooltip("Higher = focus snaps faster to the target distance. 0 = instant.")]
    public float smoothing = 8f;

    [Tooltip("Extra offset added on top of the measured distance. Negative pushes focus a bit in front of the player.")]
    public float distanceOffset = 0f;

    [Tooltip("Clamp the focus distance to avoid extreme values when the camera is very close/far.")]
    public Vector2 distanceClamp = new Vector2(0.1f, 100f);

    DepthOfField dof;

    void Awake()
    {
        if (!cam) cam = Camera.main;
        TryFetchDof();
    }

    void OnEnable()
    {
        TryFetchDof();
    }

    void TryFetchDof()
    {
        if (volume == null || volume.profile == null) return;
        volume.profile.TryGet(out dof);
    }

    void LateUpdate()
    {
        if (dof == null)
            TryFetchDof();

        if (dof == null || cam == null || target == null)
            return;

        // Use camera-forward projected distance, so strafing left/right doesn't
        // unfocus the character. (Pure Vector3.Distance also works, but it
        // shrinks slightly when the player is off-center.)
        Vector3 toTarget = target.position - cam.transform.position;
        float dist = Vector3.Dot(toTarget, cam.transform.forward) + distanceOffset;

        dist = Mathf.Clamp(dist, distanceClamp.x, distanceClamp.y);

        if (smoothing <= 0f)
        {
            dof.focusDistance.value = dist;
        }
        else
        {
            float current = dof.focusDistance.value;
            dof.focusDistance.value = Mathf.Lerp(current, dist, Time.deltaTime * smoothing);
        }
    }
}
