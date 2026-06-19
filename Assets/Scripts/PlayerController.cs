using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float walkSpeed = 5f;
    [SerializeField] private float runSpeed = 8f;
    [SerializeField] private float moveThreshold = 0.1f;

    [Header("Jump Settings")]
    [SerializeField] private float jumpForce = 7f;
    [SerializeField] private float coyoteTime = 0.2f;
    [SerializeField] private float groundCheckRadius = 0.2f;
    [SerializeField] private LayerMask groundLayer;

    private Rigidbody2D rb;
    private Vector2 moveInput;
    private bool isGrounded;
    private float coyoteTimer;
    private bool isSprinting;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    public void OnMove(InputValue value)
    {
        moveInput = value.Get<Vector2>();
    }

    public void OnJump(InputValue value)
    {
        if (value.isPressed && (isGrounded || coyoteTimer > 0))
        {
            rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
            coyoteTimer = 0; // Reset coyote timer after jumping
        }
    }

    public void OnSprint(InputValue value)
    {
        isSprinting = value.isPressed;
    }

    private void Update()
    {
        CheckGround();
    }

    private void FixedUpdate()
    {
        ApplyMovement();
    }

    private void CheckGround()
    {
        isGrounded = Physics2D.OverlapCircle(transform.position + Vector3.down * 0.1f, groundCheckRadius, groundLayer);

        if (isGrounded)
        {
            coyoteTimer = coyoteTime;
        }
        else
        {
            coyoteTimer -= Time.deltaTime;
        }
    }

    private void ApplyMovement()
    {
        // Sprint is only allowed when grounded
        float currentSpeed = (isSprinting && isGrounded) ? runSpeed : walkSpeed;
        
        // Обновляем скорость, если есть ввод или если мы на земле
        if (Mathf.Abs(moveInput.x) > moveThreshold || isGrounded)
        {
            rb.linearVelocity = new Vector2(moveInput.x * currentSpeed, rb.linearVelocity.y);
        }
    }
}