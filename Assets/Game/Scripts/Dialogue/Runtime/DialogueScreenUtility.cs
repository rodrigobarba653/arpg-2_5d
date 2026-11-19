using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// World→screen for dialogue layout. Prefers the live Cinemachine output camera
/// so EventRunner Camera Shots update bubble placement correctly.
/// </summary>
public static class DialogueScreenUtility
{
    public static Camera ResolveCamera()
    {
        int count = CinemachineBrain.ActiveBrainCount;
        Camera fallback = null;
        for (int i = 0; i < count; i++)
        {
            CinemachineBrain brain = CinemachineBrain.GetActiveBrain(i);
            if (brain == null || !brain.isActiveAndEnabled)
                continue;

            Camera output = brain.OutputCamera;
            if (output == null || !output.isActiveAndEnabled)
                continue;

            if (output.CompareTag("MainCamera"))
                return output;

            if (fallback == null)
                fallback = output;
        }

        if (fallback != null)
            return fallback;

        return Camera.main;
    }

    public static Vector2 WorldToScreen(Vector3 world)
    {
        Camera cam = ResolveCamera();
        if (cam == null)
            return new Vector2(Screen.width * 0.5f, Screen.height * 0.55f);

        Vector3 sp = cam.WorldToScreenPoint(world);
        if (sp.z < 0f)
            sp.z = 0f;
        return new Vector2(sp.x, sp.y);
    }
}
