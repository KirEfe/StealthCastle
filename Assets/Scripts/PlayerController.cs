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

    [Header("Crouch Settings")]
    [SerializeField] private float crouchSpeed = 1.5f;
    [SerializeField] private float crouchColliderHeight = 0.5f;
    [SerializeField] private float standColliderHeight = 1f;

    private Rigidbody2D rb;
    private CapsuleCollider2D capsuleCollider;
    private Vector2 moveInput;
    private bool isGrounded;
    private float coyoteTimer;
    private bool isSprinting;

    public bool IsCrouching { get; private set; }

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        capsuleCollider = GetComponent<CapsuleCollider2D>();
    }

    public void OnMove(InputValue value)
    {
        moveInput = value.Get<Vector2>();
    }

    public void OnJump(InputValue value)
    {
        // Прыжок заблокирован при приседании
        if (IsCrouching) return;

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

    public void OnCrouch(InputValue value)
    {
        if (!value.isPressed) return;
        
        if (IsCrouching)
            TryStandingUp();
        else
            StartCrouching();
    }

    private void StartCrouching()
    {
        IsCrouching = true;
        UpdateColliderHeight(crouchColliderHeight);
    }

    private void TryStandingUp()
    {
        // Вычисляем верхнюю точку текущего коллайдера
        Vector2 topPoint = (Vector2)transform.position + Vector2.up * (capsuleCollider.size.y / 2 + capsuleCollider.offset.y);
        
        // Проверяем наличие места над головой (разница между высотой в полный рост и в приседании)
        float checkDistance = standColliderHeight - crouchColliderHeight;
        RaycastHit2D hit = Physics2D.Raycast(topPoint, Vector2.up, checkDistance, groundLayer);
        
        if (hit.collider == null)
        {
            IsCrouching = false;
            UpdateColliderHeight(standColliderHeight);
        }
    }

    private void UpdateColliderHeight(float height)
    {
        if (capsuleCollider != null)
        {
            capsuleCollider.size = new Vector2(capsuleCollider.size.x, height);
            
            // Корректируем offset, чтобы низ коллайдера оставался на месте
            if (height == crouchColliderHeight)
            {
                capsuleCollider.offset = new Vector2(capsuleCollider.offset.x, -(standColliderHeight - crouchColliderHeight) / 2);
            }
            else
            {
                capsuleCollider.offset = new Vector2(capsuleCollider.offset.x, 0);
            }
        }
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
        // Если приседаем, используем скорость приседания, игнорируя спринт
        float currentSpeed = IsCrouching ? crouchSpeed : ((isSprinting && isGrounded) ? runSpeed : walkSpeed);
        
        // Обновляем скорость, если есть ввод или если мы на земле
        if (Mathf.Abs(moveInput.x) > moveThreshold || isGrounded)
        {
            rb.linearVelocity = new Vector2(moveInput.x * currentSpeed, rb.linearVelocity.y);
        }
    }
}