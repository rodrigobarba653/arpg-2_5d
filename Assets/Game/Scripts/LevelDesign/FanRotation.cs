using UnityEngine;

public class FanRotator : MonoBehaviour
{
    [Header("Rotation")]
    public bool autoRotate = true;
    public float speed = 180f;                 // degrees per second
    public Vector3 axis = Vector3.forward;     // default for a flat sprite/quad fan

    [Header("Space")]
    public bool useLocalAxis = true;           // true = axis follows object local space
    public Space rotationSpace = Space.Self;   // Self or World

    [Header("Optional Pivot")]
    public bool usePivot = false;
    public Transform pivot;                    // if null, rotates around own center
    public Vector3 pivotAxis = Vector3.forward;
    public bool useLocalPivotAxis = true;

    [Header("Direction")]
    public bool clockwise = true;              // flips direction easily

    void Update()
    {
        if (!autoRotate) return;

        float dir = clockwise ? -1f : 1f;
        float angle = speed * dir * Time.deltaTime;

        if (usePivot && pivot != null)
        {
            Vector3 worldAxis = useLocalPivotAxis
                ? pivot.TransformDirection(pivotAxis.normalized)
                : pivotAxis.normalized;

            transform.RotateAround(pivot.position, worldAxis, angle);
        }
        else
        {
            Vector3 finalAxis = useLocalAxis
                ? transform.TransformDirection(axis.normalized)
                : axis.normalized;

            if (rotationSpace == Space.World)
                transform.Rotate(finalAxis, angle, Space.World);
            else
                transform.Rotate(axis.normalized, angle, Space.Self);
        }
    }

    // ----- Quick controls you can call from other scripts / buttons -----

    public void SetSpeed(float newSpeed) => speed = newSpeed;

    public void SetAxis(Vector3 newAxis) => axis = newAxis.normalized;

    public void SetPivotAxis(Vector3 newAxis) => pivotAxis = newAxis.normalized;

    public void SetClockwise(bool value) => clockwise = value;

    public void ToggleRotation() => autoRotate = !autoRotate;

    public void ReverseDirection() => clockwise = !clockwise;
}