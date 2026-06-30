using UnityEngine;
using UnityEngine.InputSystem;
using StealthCastle.Player;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour, IPlayerLedgeContext
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
    [SerializeField] private Transform groundCheckPoint;

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
    private SpriteRenderer spriteRenderer;
    private bool isFacingRight = true;

    // Ссылка на систему маскировки
    private StealthCastle.Mechanics.DisguiseSystem disguiseSystem;
    private bool isMoving;

    // Система залезания на препятствия
    private LedgeClimbSystem ledgeClimbSystem;

    // Backing field для IsCrouching (избегаем рекурсии)
    private bool _isCrouching;
    public bool IsCrouching => _isCrouching;

    public Vector2 MoveInput => moveInput;

    // IPlayerLedgeContext implementation
    public bool IsGrounded => isGrounded;
    public bool IsDisguised
    {
        get
        {
            var ds = GetComponent<StealthCastle.Mechanics.DisguiseSystem>();
            return ds != null && ds.IsDisguised;
        }
    }
    public LayerMask ObstacleLayer => groundLayer;
    public Rigidbody2D Rigidbody => rb;
    public CapsuleCollider2D Collider => capsuleCollider;
    public Animator Animator => _animator;
    public float ColliderHeight => capsuleCollider != null ? capsuleCollider.size.y : 1f;

    private bool inputEnabled = true;

    public void SetInputEnabled(bool enabled)
    {
        inputEnabled = enabled;
        if (!enabled)
        {
            moveInput = Vector2.zero;
            isSprinting = false;

            // Сброс залезания при отключении ввода
            if (ledgeClimbSystem != null)
            {
                ledgeClimbSystem.ForceRelease();
                ledgeClimbSystem.SetInputEnabled(false);
            }

            // СТРАХОВКА: Если мы принудительно выключаем ввод (транзит между этажами), 
            // мгновенно сбрасываем маскировку, чтобы не протащить её сквозь телепорт
            if (disguiseSystem != null && disguiseSystem.IsDisguised)
            {
                disguiseSystem.RemoveDisguise("Снято при переезде между этажами");
            }
        }
        else
        {
            ledgeClimbSystem?.SetInputEnabled(true);
        }
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        capsuleCollider = GetComponent<CapsuleCollider2D>();
        disguiseSystem = GetComponent<StealthCastle.Mechanics.DisguiseSystem>();
        _animator = GetComponentInChildren<Animator>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        SyncGroundCheckPosition();

        // Инициализация системы залезания
        ledgeClimbSystem = GetComponent<LedgeClimbSystem>();
        if (ledgeClimbSystem != null)
        {
            ledgeClimbSystem.Initialize(this);
        }

        // Кэшируем исходные физические размеры вора
        if (capsuleCollider != null)
        {
            originalColliderSize = capsuleCollider.size;
            originalColliderOffset = capsuleCollider.offset;
        }
    }

    public void OnMove(InputValue value)
    {
        if (!inputEnabled) return;
        moveInput = value.Get<Vector2>();
    }

    public void OnJump(InputValue value)
    {
        if (!inputEnabled) return;

        // Если мы в состоянии залезания — делегируем в LedgeClimbSystem
        if (ledgeClimbSystem != null 
            && (ledgeClimbSystem.CurrentState == LedgeClimbSystem.LedgeState.WallRunning
                || ledgeClimbSystem.CurrentState == LedgeClimbSystem.LedgeState.Grabbing))
        {
            ledgeClimbSystem.OnJumpPressed();
            return;
        }

        // Прыжок заблокирован при приседании или маскировке
        if (_isCrouching || (disguiseSystem != null && disguiseSystem.IsDisguised)) return;

        if (value.isPressed && (isGrounded || coyoteTimer > 0))
        {
            rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
            coyoteTimer = 0;
        }

        if (_animator != null)
        _animator.SetTrigger("Jump");
    }

    public void OnSprint(InputValue value)
    {
        if (!inputEnabled) return;
        isSprinting = value.isPressed;
    }

    public void OnCrouch(InputValue value)
    {
        if (!inputEnabled) return;
        if (!value.isPressed) return;

        // Приседание заблокировано при маскировке
        if (disguiseSystem != null && disguiseSystem.IsDisguised) return;

        if (_isCrouching)
            TryStandingUp();
        else
            StartCrouching();
    }

    /// <summary>
    /// Ввод «вниз» (S / DownArrow) — отпускание края при залезании.
    /// Срабатывает ТОЛЬКО если LedgeState != None, иначе return (не перехватываем ввод для FloorTransition).
    /// </summary>
    public void OnDropDown(InputValue value)
    {
        if (!inputEnabled) return;
        if (!value.isPressed) return;

        if (ledgeClimbSystem != null && ledgeClimbSystem.CurrentState != LedgeClimbSystem.LedgeState.None)
        {
            ledgeClimbSystem.OnDropDownPressed();
            return;
        }
        // Иначе — не обрабатываем, оставляем для FloorTransition
    }

    private void StartCrouching()
    {
        _isCrouching = true;
        _animator.SetBool("IsCrouch", _isCrouching);
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
            _isCrouching = false;
            _animator.SetBool("IsCrouch", _isCrouching);
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
        SyncGroundCheckPosition();
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
        // Проверка залезания на препятствия (только если не в процессе залезания)
        if (ledgeClimbSystem != null 
            && ledgeClimbSystem.CurrentState == LedgeClimbSystem.LedgeState.None
            && moveInput.x != 0
            && !isGrounded
            && rb.linearVelocity.y <= 0f)
        {
            ledgeClimbSystem.TryGrabLedge();
        }

        // Если залезание активно — пропускаем обычное движение
        if (ledgeClimbSystem != null && ledgeClimbSystem.CurrentState != LedgeClimbSystem.LedgeState.None)
        {
            return;
        }

        ApplyMovement();
    }

    private void CheckGround()
    {
        isGrounded = Physics2D.OverlapCircle(groundCheckPoint.position, groundCheckRadius, groundLayer);

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
        float currentSpeed = _isCrouching ? crouchSpeed : ((isSprinting && isGrounded) ? runSpeed : walkSpeed);

        // Обновляем скорость, если есть ввод или если мы на земле
        if (Mathf.Abs(moveInput.x) > moveThreshold || isGrounded)
        {
            rb.linearVelocity = new Vector2(moveInput.x * currentSpeed, rb.linearVelocity.y);
        }

        if (moveInput.x != 0 && (disguiseSystem == null || !disguiseSystem.IsDisguised))
        {
            if (moveInput.x > 0 && !isFacingRight)
            {
                isFacingRight = true;
                transform.localScale = new Vector3(
                    Mathf.Abs(transform.localScale.x),
                    transform.localScale.y,
                    transform.localScale.z);
            }
            else if (moveInput.x < 0 && isFacingRight)
            {
                isFacingRight = false;
                transform.localScale = new Vector3(
                    -Mathf.Abs(transform.localScale.x),
                    transform.localScale.y,
                    transform.localScale.z);
            }
        }

        if (_animator != null)
        {
            _animator.SetFloat("Speed", Mathf.Abs(moveInput.x));
            _animator.SetBool("IsGrounded", isGrounded);
            _animator.SetFloat("VerticalVelocity", rb.linearVelocity.y);
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
        SyncGroundCheckPosition();
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
        SyncGroundCheckPosition();
    }

    /// <summary>
    /// Автоматически смещает дочерний GameObject проверки земли строго на нижнюю грань текущего коллайдера.
    /// </summary>
    private void SyncGroundCheckPosition()
    {
        if (groundCheckPoint != null && capsuleCollider != null)
        {
            // Вычисляем локальный Y для низа капсулы: offset.y минус половина высоты
            float colliderBottomLocalY = capsuleCollider.offset.y - (capsuleCollider.size.y / 2f);

            // Корректируем локальную позицию дочернего объекта
            groundCheckPoint.localPosition = new Vector3(capsuleCollider.offset.x, colliderBottomLocalY, 0f);
        }
    }
}