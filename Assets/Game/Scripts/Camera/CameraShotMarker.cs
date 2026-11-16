using UnityEngine;

/// <summary>
/// Authored cinematic shot. Designers place an empty; they do not add a
/// Cinemachine camera. Fixed uses this transform as the camera pose.
/// Follow orbits followTarget, or this transform when that target is empty.
/// </summary>
public sealed class CameraShotMarker : MonoBehaviour
{
    public enum CameraShotMode
    {
        Fixed = 0,
        Follow = 1,
    }

    public CameraShotMode mode = CameraShotMode.Fixed;

    [Tooltip("If set, the camera aims here. Fixed without this uses the marker rotation. Follow without this looks at the follow target.")]
    public Transform lookAt;

    [Tooltip("Follow mode. Empty means this transform. Fixed shots ignore it.")]
    public Transform followTarget;

    public bool overrideFieldOfView;

    [Min(1f)]
    public float fieldOfView = 45f;

    [Tooltip("Follow mode. When off, the gameplay follow offset is used.")]
    public bool overrideFollowOffset;

    public Vector3 followOffset = new Vector3(0f, 4.5f, -9.3f);

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.95f, 0.45f, 0.1f, 0.9f);
        Gizmos.DrawWireCube(transform.position, new Vector3(0.4f, 0.25f, 0.4f));
        Gizmos.DrawLine(transform.position, transform.position + transform.forward * 2f);
        if (mode == CameraShotMode.Follow && followTarget != null)
            Gizmos.DrawLine(transform.position, followTarget.position);
    }
}
