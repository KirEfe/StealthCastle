using UnityEngine;
using System;

namespace StealthCastle.Player
{
    /// <summary>
    /// Система залезания на препятствия (Ledge Climb).
    /// Две модели поведения:
    /// - Низкое препятствие (1.3-2.3): мгновенное залезание
    /// - Высокое препятствие (2.3-3.5): зависание на краю + залезание по прыжку
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(CapsuleCollider2D))]
    public class LedgeClimbSystem : MonoBehaviour
    {
        public enum LedgeState { None, Grabbing, Climbing, WallRunning }
        public LedgeState CurrentState => ledgeState;

        // События для анимаций и внешних систем
        public event Action OnLedgeGrabStart;
        public event Action OnLedgeClimbComplete;
        public event Action OnLedgeRelease;

        [Header("Ledge Climb Settings")]
        [SerializeField] private float ledgeCheckDistance = 0.5f;
        [SerializeField] private float lowLedgeMinHeight = 1.3f;
        [SerializeField] private float lowLedgeMaxHeight = 2.3f;
        [SerializeField] private float highLedgeMinHeight = 2.3f;
        [SerializeField] private float highLedgeMaxHeight = 3.5f;
        [SerializeField] private float wallRunTime = 0.3f;
        [SerializeField] private float climbUpDuration = 0.5f;
        [SerializeField] private Vector2 ledgeGrabOffset = new Vector2(0.3f, 0.1f);
        [SerializeField] private float wallRunSpeed = 4f;

        // Внутренние ссылки (через интерфейс)
        private IPlayerLedgeContext ctx;
        private Rigidbody2D rb;
        private CapsuleCollider2D capsuleCollider;
        private Animator animator;

        // Состояние машины
        private LedgeState ledgeState = LedgeState.None;
        private Vector2 ledgeGrabPoint;      // Точка захвата в мировых координатах
        private Vector2 ledgeNormal;         // Нормаль стены
        private float wallRunTimer;
        private float climbTimer;
        private Vector2 startClimbPosition;
        private Vector2 targetClimbPosition;
        private float originalGravityScale;
        private RigidbodyConstraints2D originalConstraints;
        private bool inputEnabled = true;
        private int moveDirection;           // -1 или 1, направление к стене

        /// <summary>
        /// Инициализация системы (вызывается из PlayerController.Awake)
        /// </summary>
        public void Initialize(IPlayerLedgeContext context)
        {
            ctx = context;
            rb = ctx.Rigidbody;
            capsuleCollider = ctx.Collider;
            animator = ctx.Animator;
            originalGravityScale = rb.gravityScale;
            originalConstraints = rb.constraints;
        }

        /// <summary>
        /// Включение/отключение проверок (для телепортов, маскировки и т.д.)
        /// </summary>
        public void SetInputEnabled(bool enabled)
        {
            inputEnabled = enabled;
            if (!enabled && ledgeState != LedgeState.None)
            {
                ForceRelease();
            }
        }

        public void TryGrabLedge()
        {
            if (!inputEnabled || ledgeState != LedgeState.None) return;
            if (ctx.IsGrounded || ctx.IsCrouching || ctx.IsDisguised) return;

            float dir = Mathf.Sign(ctx.MoveInput.x);
            if (dir == 0) return;
            if (rb.linearVelocity.y > 0.1f) return; // Только при падении или горизонтальном полёте

            moveDirection = (int)dir;
            Vector2 rayOrigin = GetLedgeRayOrigin();
            Vector2 rayDirection = new Vector2(moveDirection, 0);

            // 1. Ищем стену на уровне груди
            RaycastHit2D wallHit = Physics2D.Raycast(rayOrigin, rayDirection, ledgeCheckDistance, ctx.ObstacleLayer);
            if (wallHit.collider == null) return;
            ledgeNormal = wallHit.normal;

            float feetY = transform.position.y - (capsuleCollider.size.y * 0.5f);

            // 2. ПРЕДОХРАНИТЕЛЬ: Проверяем, не слишком ли высокая стена
            // Пускаем луч вперёд на максимальной высоте. Если там стена — препятствие непреодолимо!
            Vector2 highRayOrigin = new Vector2(rayOrigin.x, feetY + highLedgeMaxHeight + 0.1f);
            RaycastHit2D highWallHit = Physics2D.Raycast(highRayOrigin, rayDirection, ledgeCheckDistance, ctx.ObstacleLayer);
            if (highWallHit.collider != null) return; 

            // 3. Ищем край препятствия сверху вниз
            Vector2 topRayOrigin = new Vector2(wallHit.point.x + (moveDirection * 0.1f), feetY + highLedgeMaxHeight);
            RaycastHit2D downHit = Physics2D.Raycast(topRayOrigin, Vector2.down, highLedgeMaxHeight, ctx.ObstacleLayer);
            if (downHit.collider == null) return; // Не нашли край сверху

            // 4. Считаем высоту от НОГ игрока до найденной верхней точки
            float obstacleHeight = downHit.point.y - feetY;
            ledgeGrabPoint = downHit.point;

            // 5. Определяем тип препятствия по высоте
            if (obstacleHeight >= lowLedgeMinHeight && obstacleHeight <= lowLedgeMaxHeight)
            {
                // Низкое препятствие — сразу залезание
                StartClimbing(obstacleHeight);
            }
            else if (obstacleHeight >= highLedgeMinHeight && obstacleHeight <= highLedgeMaxHeight)
            {
                // Высокое препятствие — зависание
                StartGrabbing(obstacleHeight);
            }
        }

        /// <summary>
        /// Точка выпуска луча поиска края (с учётом смещения и направления)
        /// </summary>
        private Vector2 GetLedgeRayOrigin()
        {
            Vector2 colliderCenter = (Vector2)transform.position + capsuleCollider.offset;
            float halfWidth = capsuleCollider.size.x * 0.5f;
            float halfHeight = capsuleCollider.size.y * 0.5f;

            float x = colliderCenter.x + (halfWidth + ledgeGrabOffset.x) * moveDirection;
            float y = colliderCenter.y + halfHeight - ledgeGrabOffset.y;

            return new Vector2(x, y);
        }

        /// <summary>
        /// Запуск мгновенного залезания (низкое препятствие)
        /// </summary>
        private void StartClimbing(float obstacleHeight)
        {
            ledgeState = LedgeState.Climbing;
            climbTimer = 0f;

            // Целевая позиция — на вершине препятствия, чуть сдвинутая внутрь
            float targetX = ledgeGrabPoint.x + (moveDirection > 0 ? 0.2f : -0.2f);
            float targetY = ledgeGrabPoint.y + capsuleCollider.size.y * 0.5f + 0.1f;
            targetClimbPosition = new Vector2(targetX, targetY);
            startClimbPosition = transform.position;

            DisablePhysicsForLedge();
            OnLedgeGrabStart?.Invoke();
            UpdateAnimatorState();
        }

        /// <summary>
        /// Запуск зависания на стене (высокое препятствие)
        /// </summary>
        private void StartGrabbing(float obstacleHeight)
        {
            ledgeState = LedgeState.Grabbing;
            climbTimer = 0f;
            
            // Вычисляем НАСТОЯЩИЙ край стены, отнимая смещение луча
            float realWallEdgeX = ledgeGrabPoint.x - (moveDirection * 0.1f);
            
            // Размещаем игрока: прижимаем к стене снаружи
            float grabX = realWallEdgeX - (moveDirection * (capsuleCollider.size.x * 0.5f + 0.05f));
            
            // По Y: руки цепляются за край (смещаем центр игрока вниз от края)
            float grabY = ledgeGrabPoint.y - (capsuleCollider.size.y * 0.5f); 
            
            transform.position = new Vector2(grabX, grabY);
            
            DisablePhysicsForLedge();
            OnLedgeGrabStart?.Invoke();
            UpdateAnimatorState();
        }

        /// <summary>
        /// Запуск пробежки по стене (оставлено на случай, если захочешь вернуть эту механику)
        /// </summary>
        private void StartWallRun(float obstacleHeight)
        {
            ledgeState = LedgeState.WallRunning;
            wallRunTimer = wallRunTime;

            float grabX = ledgeGrabPoint.x - ledgeNormal.x * (capsuleCollider.size.x * 0.5f + 0.05f);
            float grabY = transform.position.y; 
            transform.position = new Vector2(grabX, grabY);

            DisablePhysicsForLedge();
            UpdateAnimatorState();
        }

        /// <summary>
        /// Вызов по нажатию прыжка во время WallRunning или Grabbing
        /// </summary>
        public void OnJumpPressed()
        {
            if (!inputEnabled) return;

            if (ledgeState == LedgeState.WallRunning)
            {
                StartClimbingFromWallRun();
            }
            else if (ledgeState == LedgeState.Grabbing)
            {
                StartClimbingFromGrab();
            }
        }

        private void StartClimbingFromWallRun()
        {
            ledgeState = LedgeState.Climbing;
            climbTimer = 0f;

            float targetX = ledgeGrabPoint.x + (moveDirection > 0 ? 0.2f : -0.2f);
            float targetY = ledgeGrabPoint.y + capsuleCollider.size.y * 0.5f + 0.1f;
            targetClimbPosition = new Vector2(targetX, targetY);
            startClimbPosition = transform.position;

            UpdateAnimatorState();
        }

        private void StartClimbingFromGrab()
        {
            ledgeState = LedgeState.Climbing;
            climbTimer = 0f;
            
            // Задаем правильную точку приземления на уступе
            float targetX = ledgeGrabPoint.x + (moveDirection > 0 ? 0.2f : -0.2f);
            float targetY = ledgeGrabPoint.y + capsuleCollider.size.y * 0.5f + 0.1f;
            targetClimbPosition = new Vector2(targetX, targetY);
            
            startClimbPosition = transform.position;
            UpdateAnimatorState();
        }

        /// <summary>
        /// Отпускание края (ввод вниз / DropDown)
        /// </summary>
        public void OnDropDownPressed()
        {
            if (ledgeState != LedgeState.None)
            {
                ReleaseLedge();
            }
        }

        public void ForceRelease()
        {
            if (ledgeState != LedgeState.None)
            {
                ReleaseLedge();
            }
        }

        private void ReleaseLedge()
        {
            ledgeState = LedgeState.None;
            EnablePhysicsAfterLedge();
            OnLedgeRelease?.Invoke();
            UpdateAnimatorState();
        }

        private void Update()
        {
            if (!inputEnabled) return;

            switch (ledgeState)
            {
                case LedgeState.WallRunning:
                    UpdateWallRun();
                    break;
                case LedgeState.Climbing:
                    UpdateClimbing();
                    break;
                case LedgeState.Grabbing:
                    UpdateGrabbing();
                    break;
            }
        }

        private void UpdateWallRun()
        {
            wallRunTimer -= Time.deltaTime;

            Vector2 pos = transform.position;
            pos.y += wallRunSpeed * Time.deltaTime;
            transform.position = pos;

            if (wallRunTimer <= 0f)
            {
                StartClimbingFromWallRun();
            }
        }

        private void UpdateClimbing()
        {
            climbTimer += Time.deltaTime;
            float t = Mathf.Clamp01(climbTimer / climbUpDuration);

            float easedT = 1f - (1f - t) * (1f - t);
            transform.position = Vector2.Lerp(startClimbPosition, targetClimbPosition, easedT);

            if (climbTimer >= climbUpDuration)
            {
                transform.position = targetClimbPosition;
                ledgeState = LedgeState.None;
                EnablePhysicsAfterLedge();
                OnLedgeClimbComplete?.Invoke();
                UpdateAnimatorState();
            }
        }

        private void UpdateGrabbing()
        {
            // Здесь можно добавить усталость от висения или покачивания, если захочешь.
            // Сейчас игрок просто статично висит и ждет прыжка.
        }

        private void DisablePhysicsForLedge()
        {
            originalConstraints = rb.constraints;
            rb.gravityScale = 0f;
            rb.linearVelocity = Vector2.zero;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation; 
            capsuleCollider.enabled = false; 
        }

        private void EnablePhysicsAfterLedge()
        {
            rb.gravityScale = originalGravityScale;
            rb.constraints = originalConstraints;
            capsuleCollider.enabled = true;
        }

        private void UpdateAnimatorState()
        {
            SafeSetBool("IsLedgeGrabbing", ledgeState == LedgeState.Grabbing);
            SafeSetBool("IsWallRunning", ledgeState == LedgeState.WallRunning);
            SafeSetBool("IsClimbingLedge", ledgeState == LedgeState.Climbing);

            if (ledgeState == LedgeState.Grabbing || ledgeState == LedgeState.WallRunning)
            {
                SafeSetTrigger("OnLedgeGrab");
            }
            else if (ledgeState == LedgeState.None)
            {
                SafeSetTrigger("OnLedgeRelease");
            }
        }

        private void SafeSetBool(string name, bool value)
        {
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                animator.SetBool(name, value);
            }
        }

        private void SafeSetTrigger(string name)
        {
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                animator.SetTrigger(name);
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying || ctx == null) return;

            Gizmos.color = Color.cyan;
            Vector2 origin = GetLedgeRayOrigin();
            Gizmos.DrawRay(origin, new Vector2(moveDirection, 0) * ledgeCheckDistance);

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(ledgeGrabPoint, 0.15f);

            Gizmos.color = ledgeState == LedgeState.None ? Color.gray : Color.green;
            Gizmos.DrawWireSphere(transform.position, 0.2f);
        }
    }
}