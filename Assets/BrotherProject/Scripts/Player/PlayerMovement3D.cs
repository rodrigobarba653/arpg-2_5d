using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class PlayerMovement3D : MonoBehaviour
{
    public float moveSpeed = 4.5f;
    public float deadZone = 0.15f;
    public bool canMove = true;
    public bool isExternalMovement = false;
    Animator animator;
   
    Rigidbody rb;
    PlayerInputActions input;
    Vector2 moveInput;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        animator = GetComponent<Animator>();

        input = new PlayerInputActions();
        input.Player.Enable();
    }

    void Update()
    {
        moveInput = input.Player.Move.ReadValue<Vector2>();

        if (moveInput.magnitude < deadZone)
            moveInput = Vector2.zero;
    }

    void FixedUpdate()
    {
        if (!canMove && !isExternalMovement)
        {
            rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
            return;
        }

        if (isExternalMovement)
            return;

        Vector3 move = new Vector3(moveInput.x, 0f, moveInput.y);

        // Proyectar movimiento sobre superficie
        if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, 1.2f))
        {
            move = Vector3.ProjectOnPlane(move, hit.normal);
        }

        move *= moveSpeed;

        // Añadir pequeña fuerza hacia abajo para mantenerse pegado
        Vector3 velocity = new Vector3(move.x, rb.linearVelocity.y, move.z);

        if (Physics.Raycast(transform.position, Vector3.down, 1.1f))
        {
            velocity.y = -5f;
        }

        rb.linearVelocity = velocity;
    }



    void OnEnable() => input?.Player.Enable();
    void OnDisable() => input?.Player.Disable();
}
