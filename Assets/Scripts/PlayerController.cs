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
    private Vector2 originalColliderSize;
    private Vector2 originalColliderOffset;
    private Vector2 moveInput;
    private bool isGrounded;
    private float coyoteTimer;
    private bool isSprinting;
    private Animator _animator;


    // Ссылка на систему маскировки
    private StealthCastle.Mechanics.DisguiseSystem disguiseSystem;
    private bool isMoving;

    public bool IsCrouching { get; private set; }

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        capsuleCollider = GetComponent<CapsuleCollider2D>();
        disguiseSystem = GetComponent<StealthCastle.Mechanics.DisguiseSystem>();
        _animator = GetComponent<Animator>();
        

        // Кэшируем исходные физические размеры вора
        if (capsuleCollider != null)
        {
            originalColliderSize = capsuleCollider.size;
            originalColliderOffset = capsuleCollider.offset;
        }
    }

    public void OnMove(InputValue value)
    {
        moveInput = value.Get<Vector2>();
    }

    public void OnJump(InputValue value)
    {
        // Прыжок заблокирован при приседании или маскировке
        if (IsCrouching || (disguiseSystem != null && disguiseSystem.IsDisguised)) return;

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

        // Приседание заблокировано при маскировке
        if (disguiseSystem != null && disguiseSystem.IsDisguised) return;
        
        if (IsCrouching)
            TryStandingUp();
        else
            StartCrouching();
    }

    private void StartCrouching()
    {
        IsCrouching = true;
        _animator.SetBool("IsCrouch", IsCrouching);
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
            _animator.SetBool("IsCrouch", IsCrouching);
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

        // Проверка движения для сброса маскировки
        isMoving = moveInput.magnitude > moveThreshold;

        // Сброс маскировки при начале движения
        if (disguiseSystem != null && isMoving && disguiseSystem.IsDisguised)
        {
            disguiseSystem.RemoveDisguise("Снято при движении");
        }
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

// Адаптация коллайдера под форму маскировки
/// <summary>
/// Применяет новые размеры и смещение коллайдера, выравнивая нижнюю точку с уровнем земли.
/// </summary>
/// <param name="size">Новый размер коллайдера.</param>
/// <param name="offset">Смещение относительно центра (по X и Y).</param>
    public void AdaptColliderToDisguise(Vector2 targetSize, Vector2 targetOffset)
    {
        if (capsuleCollider == null) return;

        capsuleCollider.size = targetSize;
        capsuleCollider.offset = targetOffset;
        
        // Сбрасываем скорость, чтобы объект при маскировке не "скользил" по инерции
        rb.linearVelocity = Vector2.zero; 
    }

// Восстановление оригинального коллайдера
/// <summary>
/// Возвращает оригинальные параметры коллайдера, если над головой нет препятствия.
/// </summary>
    public void ResetColliderToNormal()
    {
        if (capsuleCollider == null) return;

        // Проверяем наличие потолка над головой
        float topY = transform.position.y + originalColliderOffset.y + originalColliderSize.y / 2f;
        RaycastHit2D hit = Physics2D.Raycast(new Vector2(transform.position.x, topY), Vector2.up, 0.1f, groundLayer);
        if (hit.collider != null)
        {
            // Если потолок есть, откладываем восстановление (можно реализовать флаг и проверять в Update)
            return;
        }

        capsuleCollider.size = originalColliderSize;
        capsuleCollider.offset = originalColliderOffset;
    }
    }