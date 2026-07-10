using UnityEngine;

/// <summary>
/// Drop on the Main Camera and on the CinemachineCamera in the title scene so
/// they survive scene loads (DontDestroyOnLoad). If a destination scene has
/// its own camera with the same kind, the destination's copy self-destroys —
/// avoiding the "2 audio listeners / 2 cameras" warning.
///
/// Pair with CameraAutoTarget on the vcam so the player is re-acquired after
/// each scene transition.
/// </summary>
public class PersistentCamera : MonoBehaviour
{
    public enum Kind { MainCamera, Vcam }

    [Tooltip("Which slot this camera occupies in the global singleton table.")]
    public Kind kind = Kind.MainCamera;

    [Tooltip("Print info on Awake / Destroy.")]
    public bool debugLog = true;

    public static PersistentCamera MainCameraInstance { get; private set; }
    public static PersistentCamera VcamInstance { get; private set; }

    void Awake()
    {
        if (kind == Kind.MainCamera)
        {
            if (MainCameraInstance != null && MainCameraInstance != this)
            {
                if (debugLog)
                    Debug.Log($"[PersistentCamera] Duplicate MainCamera in scene " +
                              $"'{gameObject.scene.name}' — destroying this one.", this);
                Destroy(gameObject);
                return;
            }
            MainCameraInstance = this;
        }
        else // Vcam
        {
            if (VcamInstance != null && VcamInstance != this)
            {
                if (debugLog)
                    Debug.Log($"[PersistentCamera] Duplicate Vcam in scene " +
                              $"'{gameObject.scene.name}' — destroying this one.", this);
                Destroy(gameObject);
                return;
            }
            VcamInstance = this;
        }

        // DontDestroyOnLoad only works for root GameObjects. If this isn't at
        // the root, warn the user — the persistence won't actually happen.
        if (transform.parent == null)
        {
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Debug.LogWarning($"[PersistentCamera] '{name}' is not at the root of " +
                             $"its scene. DontDestroyOnLoad will fail. Move it to " +
                             $"the root or this camera will NOT persist.", this);
        }

        if (debugLog)
            Debug.Log($"[PersistentCamera] Registered {kind} '{name}'.", this);
    }

    void OnDestroy()
    {
        if (MainCameraInstance == this) MainCameraInstance = null;
        if (VcamInstance == this)       VcamInstance = null;
    }
}
