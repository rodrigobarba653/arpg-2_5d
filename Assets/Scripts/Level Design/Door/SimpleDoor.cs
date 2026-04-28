using UnityEngine;
using System.Collections;

public class SimpleDoor : MonoBehaviour
{
    [Header("Mode")]
    public bool useRotate = true;
    public bool useSlideUp = false;

    [Header("Rotate Door")]
    public float openAngle = 90f;
    public float rotateSpeed = 3f;

    [Header("Slide Door")]
    public float openHeight = 3f;
    public float moveSpeed = 3f;

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
        // 🔥 asegura que solo uno esté activo
        if (useRotate && useSlideUp)
        {
            useSlideUp = false;
        }
    }

    void Start()
    {
        closedPosition = transform.position;
        closedRotation = transform.rotation;

        openRotation = Quaternion.Euler(
            transform.eulerAngles + new Vector3(0f, openAngle, 0f)
        );

        openPosition = closedPosition + transform.up * openHeight;
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

        isOpen = true;
        isMoving = false;
    }
}