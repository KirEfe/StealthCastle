using UnityEngine;

namespace StealthCastle.Isometric3D.Player
{
    /// <summary>
    /// Контроллер игрока для 3D изометрической версии.
    /// Поддерживает 8-направленное движение, маскировку и взаимодействие с объектами.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(CapsuleCollider))]
    public class PlayerController3D : MonoBehaviour
    {
        [Header("Movement Settings")]
        [SerializeField] private float walkSpeed = 5f;
        [SerializeField] private float runSpeed = 8f;
        [SerializeField] private float crawlSpeed = 1.5f; // Скорость в маскировке
        [SerializeField] private float moveThreshold = 0.1f;
        [SerializeField] private float acceleration = 60f; // м/с² — до полной скорости шага за ~0.1 с
        [SerializeField] private float deceleration = 80f; // м/с² — остановка без скольжения

        [Header("Isometric Settings")]
        [SerializeField] private float isoYScale = 0.5774f; // Коррекция Y оси для изометрии (~1/√3)
        [SerializeField] private Vector2 isoAxisScale = new Vector2(1f, 0.7071f);

        [Header("Physics Settings")]
        [SerializeField] private float groundCheckDistance = 0.2f;
        [SerializeField] private LayerMask groundLayer;
        [SerializeField] private Transform groundCheckPoint;

        [Header("Visual Settings")]
        [SerializeField] private Transform visualTransform; // Визуальная модель игрока
        [SerializeField] private float visualRotationSpeed = 10f;

        private Rigidbody rb;
        private CapsuleCollider capsuleCollider;
        private Core.InputSystem3D inputSystem;
        private Mechanics.DisguiseSystem3D disguiseSystem;
        private Core.CameraSystem3D cameraSystem;

        private Vector2 moveInput;
        private bool isGrounded;
        private bool inputEnabled = true;
        private bool isMoving;
        private float currentSpeed;

        /// <summary>Движется ли игрок</summary>
        public bool IsMoving => isMoving;

        /// <summary>Замаскирован ли игрок</summary>
        public bool IsDisguised => disguiseSystem != null && disguiseSystem.IsDisguised;

        /// <summary>Скорость ходьбы</summary>
        public float WalkSpeed => walkSpeed;

        /// <summary>Скорость бега</summary>
        public float RunSpeed => runSpeed;

        /// <summary>Скорость в маскировке</summary>
        public float CrawlSpeed => crawlSpeed;

        /// <summary>Текущая скорость</summary>
        public float CurrentSpeed => currentSpeed;

        /// <summary>Rigidbody игрока</summary>
        public Rigidbody Rigidbody => rb;

        /// <summary>Collider игрока</summary>
        public CapsuleCollider Collider => capsuleCollider;

        /// <summary>Визуальный transform игрока</summary>
        public Transform VisualTransform => visualTransform;

        /// <summary>Назначение визуальной модели (для игрока, созданного из кода)</summary>
        public void SetVisualTransform(Transform visual) => visualTransform = visual;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            capsuleCollider = GetComponent<CapsuleCollider>();
            inputSystem = FindObjectOfType<Core.InputSystem3D>();
            disguiseSystem = GetComponent<Mechanics.DisguiseSystem3D>();
            cameraSystem = FindObjectOfType<Core.CameraSystem3D>();

            // Настройка физики для изометрии (без гравитации)
            rb.useGravity = false;
            // Движение только по полу: толчок от столкновения (край ящика, угол стены) без гравитации
            // и демпфирования иначе уносит капсулу вверх навсегда
            rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;
            rb.linearDamping = 0f; // Скорость задаём сами (ApplyMovement), демпфирование бы с ней спорило
            // Физика идёт на 50 Гц, кадры чаще: без интерполяции модель и камера двигаются рывками
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            // Инициализация позиции ground check
            if (groundCheckPoint == null && capsuleCollider != null)
            {
                CreateGroundCheckPoint();
            }
        }

        private void CreateGroundCheckPoint()
        {
            var checkPoint = new GameObject("GroundCheckPoint");
            checkPoint.transform.SetParent(transform, false);
            
            // Позиционируем внизу коллайдера
            float colliderBottom = -capsuleCollider.height / 2f + capsuleCollider.radius;
            checkPoint.transform.localPosition = new Vector3(0f, colliderBottom, 0f);
            
            groundCheckPoint = checkPoint.transform;
        }

        private void Update()
        {
            if (!inputEnabled) return;

            // Чтение ввода
            ReadInput();
            
            // Обновление состояния
            UpdateState();

            // Поворот модели — каждый кадр, а не с частотой физики
            UpdateVisual();
        }

        private void FixedUpdate()
        {
            if (!inputEnabled) return;

            // Применение движения
            ApplyMovement();
            
            // Проверка земли
            CheckGround();
        }

        /// <summary>
        /// Чтение ввода от системы ввода
        /// </summary>
        private void ReadInput()
        {
            if (inputSystem == null) return;

            moveInput = inputSystem.MoveInput;
        }

        /// <summary>
        /// Обновление состояния игрока
        /// </summary>
        private void UpdateState()
        {
            // Проверка движения
            isMoving = moveInput.sqrMagnitude > moveThreshold * moveThreshold;
        }

        /// <summary>
        /// Применение движения к игроку
        /// </summary>
        private void ApplyMovement()
        {
            // Определяем текущую скорость
            if (disguiseSystem != null && disguiseSystem.IsDisguised)
            {
                currentSpeed = crawlSpeed; // Медленнее в маскировке
            }
            else if (inputSystem != null && inputSystem.SprintPressed)
            {
                currentSpeed = runSpeed;
            }
            else
            {
                currentSpeed = walkSpeed;
            }

            // Плавный, но быстрый выход на целевую скорость: без мгновенных скачков при старте и смене направления
            Vector3 target = isMoving ? GetIsometricMoveDirection() * currentSpeed : Vector3.zero;
            Vector3 current = rb.linearVelocity;
            current.y = 0f;

            float rate = isMoving ? acceleration : deceleration;
            Vector3 velocity = Vector3.MoveTowards(current, target, rate * Time.fixedDeltaTime);
            velocity.y = 0f; // Только по полу
            rb.linearVelocity = velocity;
        }

        /// <summary>
        /// Получение направления движения с учетом изометрии
        /// </summary>
        private Vector3 GetIsometricMoveDirection()
        {
            if (cameraSystem != null)
            {
                // Получаем направления камеры
                Vector3 cameraForward = cameraSystem.GetCameraForwardDirection();
                cameraForward.y = 0f;
                cameraForward.Normalize();
                
                Vector3 cameraRight = Vector3.Cross(Vector3.up, cameraForward);
                
                // Преобразуем 2D ввод в 3D направление относительно камеры
                return (cameraRight * moveInput.x + cameraForward * moveInput.y).normalized;
            }
            else
            {
                // Фолбэк: стандартная изометрическая коррекция
                return new Vector3(
                    moveInput.x * isoAxisScale.x,
                    0f,
                    moveInput.y * isoAxisScale.y
                ).normalized;
            }
        }

        /// <summary>
        /// Проверка нахождения на земле
        /// </summary>
        private void CheckGround()
        {
            if (groundCheckPoint == null) return;

            RaycastHit hit;
            isGrounded = Physics.Raycast(
                groundCheckPoint.position,
                Vector3.down,
                out hit,
                groundCheckDistance,
                groundLayer
            );
        }

        /// <summary>
        /// Обновление визуальной модели игрока
        /// </summary>
        private void UpdateVisual()
        {
            if (visualTransform == null) return;

            if (isMoving)
            {
                // Поворот визуала в направлении движения
                Vector3 moveDirection = GetIsometricMoveDirection();
                if (moveDirection.sqrMagnitude > 0.01f)
                {
                    // Вычисляем целевой угол поворота
                    float targetAngle = Mathf.Atan2(moveDirection.x, moveDirection.z) * Mathf.Rad2Deg;
                    
                    // Плавный поворот
                    Quaternion targetRotation = Quaternion.Euler(0f, targetAngle, 0f);
                    visualTransform.rotation = Quaternion.Slerp(
                        visualTransform.rotation,
                        targetRotation,
                        visualRotationSpeed * Time.deltaTime
                    );
                }
            }
        }

        /// <summary>
        /// Включение/отключение ввода
        /// </summary>
        public void SetInputEnabled(bool enabled)
        {
            inputEnabled = enabled;
            
            if (!enabled)
            {
                moveInput = Vector2.zero;
                rb.linearVelocity = Vector3.zero;
                
                // Сброс маскировки при отключении ввода
                if (disguiseSystem != null && disguiseSystem.IsDisguised)
                {
                    disguiseSystem.RemoveDisguise("Input disabled");
                }
            }
        }

        /// <summary>
        /// Установка скорости движения
        /// </summary>
        public void SetMoveSpeed(float speed)
        {
            walkSpeed = speed;
        }

        /// <summary>
        /// Установка скорости бега
        /// </summary>
        public void SetRunSpeed(float speed)
        {
            runSpeed = speed;
        }

        /// <summary>
        /// Установка скорости в маскировке
        /// </summary>
        public void SetCrawlSpeed(float speed)
        {
            crawlSpeed = speed;
        }

        /// <summary>
        /// Телепорт игрока в указанную позицию
        /// </summary>
        public void TeleportTo(Vector3 position)
        {
            // Через rb.position — мгновенный перенос, интерполяция не рисует пролёт через уровень
            rb.position = position;
            transform.position = position;
            rb.linearVelocity = Vector3.zero;
        }

        /// <summary>
        /// Получение позиции игрока
        /// </summary>
        public Vector3 GetPosition()
        {
            return transform.position;
        }

        /// <summary>
        /// Получение направления взгляда игрока
        /// </summary>
        public Vector3 GetFacingDirection()
        {
            if (visualTransform != null)
            {
                return visualTransform.forward;
            }
            return transform.forward;
        }

        /// <summary>
        /// Проверка, смотрит ли игрок на объект
        /// </summary>
        public bool IsFacingObject(GameObject obj, float angleThreshold = 45f)
        {
            if (obj == null) return false;

            Vector3 toObject = obj.transform.position - transform.position;
            toObject.y = 0f;
            toObject.Normalize();
            
            Vector3 facing = GetFacingDirection();
            facing.y = 0f;
            facing.Normalize();
            
            float angle = Vector3.Angle(facing, toObject);
            return angle <= angleThreshold;
        }

        /// <summary>
        /// Рисование отладочных линий
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            if (!isActiveAndEnabled) return;

            // Направление движения
            Gizmos.color = Color.cyan;
            Vector3 moveDirection = GetIsometricMoveDirection();
            Gizmos.DrawRay(transform.position, moveDirection * 2f);

            // Позиция ground check
            if (groundCheckPoint != null)
            {
                Gizmos.color = isGrounded ? Color.green : Color.red;
                Gizmos.DrawSphere(groundCheckPoint.position, 0.1f);
                Gizmos.DrawLine(groundCheckPoint.position, groundCheckPoint.position + Vector3.down * groundCheckDistance);
            }
        }
    }
}