using UnityEngine;
using System.Collections;

public class SimpleDoor : MonoBehaviour
{
    [Header("Mode")]
    public bool useRotate = true;
    public bool useSlideUp = false;
    public bool useSlideHorizontal = false;

    [Header("Rotate Door")]
    public float openAngle = 90f;
    public float rotateSpeed = 3f;

    [Header("Slide Vertical")]
    public float openHeight = 3f;
    public float moveSpeed = 3f;

    [Header("Slide Horizontal")]
    public float openDistance = 3f;
    public bool slideToRight = true; // true = derecha, false = izquierda

    [Header("Interaction")]
    public bool openOnTrigger = true;

    bool isOpen;
    bool isMoving;

    Vector3 closedPosition;
    Vector3 openPosition;

    Quaternion closedRotation;
    Quaternion openRotation;

    void OnValidate()
    {
        // 🔥 asegurar que solo un modo esté activo
        int activeModes = 0;
        if (useRotate) activeModes++;
        if (useSlideUp) activeModes++;
        if (useSlideHorizontal) activeModes++;

        if (activeModes > 1)
        {
            // prioridad: Rotate > SlideUp > SlideHorizontal
            if (useRotate)
            {
                useSlideUp = false;
                useSlideHorizontal = false;
            }
            else if (useSlideUp)
            {
                useSlideHorizontal = false;
            }
        }
    }

    void Start()
    {
        closedPosition = transform.position;
        closedRotation = transform.rotation;

        // ROTATE
        openRotation = Quaternion.Euler(
            transform.eulerAngles + new Vector3(0f, openAngle, 0f)
        );

        // SLIDE UP
        if (useSlideUp)
        {
            openPosition = closedPosition + transform.up * openHeight;
        }

        // SLIDE HORIZONTAL
        if (useSlideHorizontal)
        {
            Vector3 dir = slideToRight ? transform.right : -transform.right;
            openPosition = closedPosition + dir * openDistance;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!openOnTrigger) return;

        if (other.CompareTag("Player"))
        {
            OpenDoor();
        }
    }

    public void OpenDoor()
    {
        if (isOpen || isMoving) return;

        StartCoroutine(OpenRoutine());
    }

    IEnumerator OpenRoutine()
    {
        isMoving = true;

        // 🔥 ROTATE
        if (useRotate)
        {
            Debug.Log("ROTATE");

            while (Quaternion.Angle(transform.rotation, openRotation) > 0.5f)
            {
                transform.rotation = Quaternion.Lerp(
                    transform.rotation,
                    openRotation,
                    Time.deltaTime * rotateSpeed
                );

                yield return null;
            }

            transform.rotation = openRotation;
        }

        // 🔥 SLIDE UP
        else if (useSlideUp)
        {
            Debug.Log("SLIDE UP");

            while (Vector3.Distance(transform.position, openPosition) > 0.01f)
            {
                transform.position = Vector3.MoveTowards(
                    transform.position,
                    openPosition,
                    moveSpeed * Time.deltaTime
                );

                yield return null;
            }

            transform.position = openPosition;
        }

        // 🔥 SLIDE HORIZONTAL
        else if (useSlideHorizontal)
        {
            Debug.Log("SLIDE HORIZONTAL");

            while (Vector3.Distance(transform.position, openPosition) > 0.01f)
            {
                transform.position = Vector3.MoveTowards(
                    transform.position,
                    openPosition,
                    moveSpeed * Time.deltaTime
                );

                yield return null;
            }

            transform.position = openPosition;
        }

        isOpen = true;
        isMoving = false;
    }
}