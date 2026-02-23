using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMove : MonoBehaviour
{
    public float moveSpeed = 5f;
    public float rotationSpeed = 12f;

    private Vector2 moveInput;
    private Camera mainCam;

    void Start()
    {
        mainCam = Camera.main;
    }

    public void OnMove(InputAction.CallbackContext context)
    {
        moveInput = context.ReadValue<Vector2>();
    }

    void Update()
    {
        if (mainCam == null) mainCam = Camera.main;
        if (moveInput.sqrMagnitude < 0.01f) return;

        // Camera-relative directions, flattened to ground
        Vector3 camForward = mainCam.transform.forward; camForward.y = 0f; camForward.Normalize();
        Vector3 camRight   = mainCam.transform.right;   camRight.y = 0f;   camRight.Normalize();

        Vector3 moveDir = (camForward * moveInput.y + camRight * moveInput.x);
        if (moveDir.sqrMagnitude > 1f) moveDir.Normalize();

        // Move
        transform.position += moveDir * moveSpeed * Time.deltaTime;

        // Rotate Y-only (no tilt/roll)
        float yaw = Mathf.Atan2(moveDir.x, moveDir.z) * Mathf.Rad2Deg;
        Quaternion targetRot = Quaternion.Euler(0f, yaw, 0f);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
    }
}