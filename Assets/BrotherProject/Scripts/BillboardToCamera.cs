using UnityEngine;

public class BillboardToCamera : MonoBehaviour
{
    Camera cam;

    void Start()
    {
        cam = Camera.main;
    }

    void LateUpdate()
    {
        if (!cam) return;

        Vector3 lookDir = transform.position - cam.transform.position;
        lookDir.y = 0f; // evita que se incline

        transform.rotation = Quaternion.LookRotation(lookDir);
    }
}
