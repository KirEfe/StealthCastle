using System;
using System.Collections.Generic;
using UnityEngine;

namespace StealthCastle.Isometric3D.Enemies
{
    /// <summary>
    /// ИИ врага для 3D изометрической версии.
    /// Поддерживает патрулирование, обнаружение, погоню и поиск.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class EnemyAI3D : MonoBehaviour
    {
        public enum EnemyState { Patrol, Investigate, Chase, Search }

        [Header("Patrol Settings")]
        [Tooltip("Маршрут патруля: отдельный объект с точками-детьми, точки ставятся руками в сцене")]
        [SerializeField] private World.PatrolRoute3D route;
        [SerializeField] private List<Vector3> waypoints = new List<Vector3>(); // Запасной вариант без маршрута (тестовые сцены)
        [SerializeField] private bool waypointsInParentSpace = false; // Точки в координатах родителя (куска уровня) — для префабов кусков
        [SerializeField] private float patrolSpeed = 2f;
        [SerializeField] private float waypointReachDistance = 0.3f;
        [SerializeField] private float waitAtWaypoint = 2f;

        [Header("Detection Settings")]
        [SerializeField] private float visionRange = 5f; // Радиус обнаружения
        [SerializeField, Range(10f, 120f)] private float fovDegrees = 45f; // Угол обзора
        [SerializeField] private float chaseSpeed = 4f;
        [SerializeField] private float crawlDetectDistance = 2f; // Дистанция обнаружения ползущего
        [SerializeField] private float loseSightTime = 2f; // Время потери из виду

        [Header("Search Settings")]
        [SerializeField] private float searchDuration = 4f;

        [Header("Suspicion Settings")]
        [SerializeField] private float suspicionRange = 2.5f;
        [SerializeField] private float propCheckCooldown = 3f;
        [SerializeField] private float lookBackDuration = 1f; // Сколько смотрит назад после разворота на точке

        [Header("Turning")]
        [SerializeField] private float turnSpeed = 200f; // Поворот взгляда, град/с: разворот, осмотр, смена направления
        [SerializeField] private float chaseTurnSpeed = 400f; // В погоне резче, но без скачков

        [Header("Physics Settings")]
        [SerializeField] private LayerMask wallMask; // Слой стен
        [SerializeField] private LayerMask obstacleMask; // Слой препятствий

        [Header("References")]
        [SerializeField] private Transform playerTransform; // Трансформация игрока
        [SerializeField] private Transform visualTransform; // Визуальная модель врага

        private EnemyState state = EnemyState.Patrol;
        private Rigidbody rb;
        private int currentWaypoint = 0;
        private int routeDirection = 1; // Направление обхода для маршрута «туда-обратно»
        private bool patrolStarted;
        private float waitTimer;
        private float searchTimer;
        private float noSightTimer;
        private float propCheckTimer;
        private float lookBackTimer;
        private float searchSpinTimer;

        // Взгляд стража (угол по Y). Один на всё: проверка видимости, конус на полу и модель —
        // поворачивается плавно, поэтому игрок всегда видит, куда страж посмотрит дальше
        private float facingYaw;
        private float desiredYaw;

        private Vector3 investigatePoint;
        private Vector3 lastKnownPlayerPos;
        private Vector3 lastMoveDirection;
        private Vector2 lookDirection;

        private Mechanics.DisguiseSystem3D playerDisguise;
        private Player.PlayerController3D playerController;

        public System.Action OnPlayerCaught; // Событие поимки игрока

        /// <summary>Текущее состояние врага</summary>
        public EnemyState CurrentState => state;

        /// <summary>Целевой трансформ игрока</summary>
        public Transform PlayerTransform
        {
            get => playerTransform;
            set
            {
                playerTransform = value;
                if (playerTransform != null)
                {
                    playerDisguise = playerTransform.GetComponent<Mechanics.DisguiseSystem3D>();
                    playerController = playerTransform.GetComponent<Player.PlayerController3D>();
                }
            }
        }

        /// <summary>Направление взгляда</summary>
        public Vector2 LookDirection => lookDirection;

        /// <summary>Дальность зрения</summary>
        public float VisionRange => visionRange;

        /// <summary>Угол обзора</summary>
        public float FovDegrees => fovDegrees;

        /// <summary>Слой, перекрывающий обзор</summary>
        public LayerMask ObstacleMask => obstacleMask;

        /// <summary>
        /// Направление взгляда в мире. Одно и то же для проверки видимости и для конуса зрения,
        /// чтобы игрок видел ровно ту зону, в которой его замечают
        /// </summary>
        public Vector3 ViewForward => Quaternion.Euler(0f, facingYaw, 0f) * Vector3.forward;

        /// <summary>Угол направления в плане (по Y), градусы</summary>
        private static float YawOf(Vector3 direction) => Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            
            // Настройка физики
            rb.useGravity = false;
            rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY; // Только по полу
            rb.linearDamping = 5f;
            // Плавное движение модели и конуса между шагами физики (50 Гц)
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            // Начальное направление взгляда — куда повёрнута модель в куске уровня
            Vector3 initialForward = visualTransform != null ? visualTransform.forward : transform.forward;
            facingYaw = desiredYaw = YawOf(initialForward);
            lookDirection = new Vector2(ViewForward.x, ViewForward.z);

            // Поиск игрока
            if (playerTransform == null)
            {
                var playerObj = GameObject.FindGameObjectWithTag("Player");
                if (playerObj != null)
                {
                    PlayerTransform = playerObj.transform;
                }
            }
            else
            {
                // Назначен в инспекторе: сеттер не вызывался, кешируем маскировку и контроллер игрока.
                // Без этого страж игнорирует маскировку
                PlayerTransform = playerTransform;
            }
        }

        private void Update()
        {
            // Вне игры (сборка уровня, экран победы или поимки) страж не смотрит и не реагирует
            if (playerTransform == null || !IsRunActive()) return;

            // Декремент таймеров
            if (propCheckTimer > 0) propCheckTimer -= Time.deltaTime;
            // Взгляд назад и шаги осмотра отсчитываются, когда страж уже довернулся, а не с начала поворота
            bool turned = Mathf.Abs(Mathf.DeltaAngle(facingYaw, desiredYaw)) < 2f;
            if (lookBackTimer > 0 && turned) lookBackTimer -= Time.deltaTime;
            if (searchSpinTimer > 0 && turned) searchSpinTimer -= Time.deltaTime;

            // Обнаружение игрока (кроме состояния погони); во вступлении уровня — только патруль
            if (state != EnemyState.Chase && IsDetectionActive())
            {
                LookForPlayer();
            }

            // Обновление визуала
            UpdateVisual();
        }

        private void FixedUpdate()
        {
            // Кинематика — кусок уровня ещё поднимается из-под пола, движение ждёт
            if (playerTransform == null || rb.isKinematic || !IsRunActive()) return;

            switch (state)
            {
                case EnemyState.Patrol:
                    UpdatePatrol();
                    break;
                case EnemyState.Investigate:
                    UpdateInvestigate();
                    break;
                case EnemyState.Chase:
                    UpdateChase();
                    break;
                case EnemyState.Search:
                    UpdateSearch();
                    break;
            }
        }

        /// <summary>
        /// Обновление патрулирования
        /// </summary>
        private void UpdatePatrol()
        {
            if (WaypointCount == 0)
            {
                rb.linearVelocity = Vector3.zero;
                return;
            }

            // После точки страж стоит и смотрит назад, иначе MoveTowards сразу сбросит обзор
            if (lookBackTimer > 0f)
            {
                rb.linearVelocity = Vector3.zero;
                return;
            }

            // Страж стоит на первой точке с самого начала — сразу идёт к следующей, а не ждёт:
            // во вступлении уровня игрок должен видеть маршруты в движении
            if (!patrolStarted)
            {
                patrolStarted = true;
                if (WaypointCount > 1 && FlatDistance(transform.position, GetWaypoint(currentWaypoint)) < waypointReachDistance)
                {
                    currentWaypoint = NextWaypoint(currentWaypoint);
                }
            }

            Vector3 target = GetWaypoint(currentWaypoint);
            float distanceToTarget = FlatDistance(transform.position, target);

            if (distanceToTarget < waypointReachDistance)
            {
                rb.linearVelocity = Vector3.zero;
                waitTimer += Time.fixedDeltaTime;
                
                if (waitTimer >= waitAtWaypoint)
                {
                    waitTimer = 0f;
                    LookBackAtWaypoint();
                    currentWaypoint = NextWaypoint(currentWaypoint);
                }
                return;
            }

            if (IsDetectionActive()) MaybeInspectProp();
            MoveTowards(target, patrolSpeed);
        }

        /// <summary>
        /// Обновление расследования
        /// </summary>
        private void UpdateInvestigate()
        {
            float distanceToTarget = FlatDistance(transform.position, investigatePoint);

            if (distanceToTarget < waypointReachDistance)
            {
                rb.linearVelocity = Vector3.zero;
                EnterSearch();
                return;
            }

            MoveTowards(investigatePoint, patrolSpeed);
        }

        /// <summary>
        /// Обновление погони
        /// </summary>
        private void UpdateChase()
        {
            if (playerTransform == null) return;

            // Проверка маскировки игрока
            if (playerDisguise != null && playerDisguise.IsDisguised && !CanSeePlayer())
            {
                lastKnownPlayerPos = playerTransform.position;
                EnterSearch();
                return;
            }

            if (CanSeePlayer())
            {
                lastKnownPlayerPos = playerTransform.position;
                noSightTimer = 0f; // Видит — погоня продолжается
            }

            MoveTowards(lastKnownPlayerPos, chaseSpeed);

            // Поимка игрока
            if (FlatDistance(transform.position, playerTransform.position) < 0.6f)
            {
                // Проверка на активный забег
                var gameManager = Core.GameManager3D.Instance;
                if (gameManager != null && gameManager.CurrentState == Core.GameManager3D.GameState.Playing && !Core.DebugFlags3D.GodMode)
                {
                    OnPlayerCaught?.Invoke();
                    gameManager.GameOver();
                }
                return;
            }

            noSightTimer += Time.fixedDeltaTime;
            if (noSightTimer >= loseSightTime)
            {
                EnterSearch();
            }
        }

        /// <summary>
        /// Обновление поиска
        /// </summary>
        private void UpdateSearch()
        {
            rb.linearVelocity = Vector3.zero;

            // Осмотр на месте: плавный поворот на 90°, пауза, следующий
            if (searchSpinTimer <= 0f)
            {
                searchSpinTimer = 0.5f;
                desiredYaw += 90f;
            }

            searchTimer -= Time.fixedDeltaTime;
            if (searchTimer <= 0f)
            {
                state = EnemyState.Patrol;
                currentWaypoint = FindNearestWaypoint();
            }
        }

        /// <summary>
        /// Поиск игрока
        /// </summary>
        private void LookForPlayer()
        {
            if (playerTransform == null) return;

            float distance = FlatDistance(transform.position, playerTransform.position);

            if (playerDisguise != null && playerDisguise.IsDisguised)
            {
                // Неподвижный замаскированный игрок невидим; ползущего видно
                // только вблизи и только в конусе зрения
                if (playerDisguise.IsMoving && distance <= crawlDetectDistance && CanSeePlayer())
                {
                    TriggerAlert();
                }
                return;
            }

            if (distance > visionRange) return;

            if (CanSeePlayer())
            {
                TriggerAlert();
            }
        }

        /// <summary>
        /// Проверка видимости игрока
        /// </summary>
        private bool CanSeePlayer()
        {
            if (playerTransform == null) return false;

            Vector3 toPlayer = playerTransform.position - transform.position;
            float distance = toPlayer.magnitude;

            if (distance > visionRange) return false;

            // Проверка угла обзора (с учётом обзора назад)
            float angle = Vector3.Angle(ViewForward, toPlayer);
            if (angle > fovDegrees * 0.5f) return false;

            // Проверка препятствий
            RaycastHit hit;
            if (Physics.Raycast(
                transform.position,
                toPlayer.normalized,
                out hit,
                distance,
                obstacleMask
            ))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Движение к цели
        /// </summary>
        private void MoveTowards(Vector3 target, float speed)
        {
            Vector3 direction = target - transform.position;
            direction.y = 0f; // Двигаемся только по полу
            if (direction.sqrMagnitude < 0.0001f) return;

            direction.Normalize();

            // Обход стен
            RaycastHit hit;
            if (Physics.Raycast(
                transform.position,
                direction,
                out hit,
                0.8f,
                wallMask
            ))
            {
                // Скользим вдоль стены
                Vector3 perp = Vector3.Cross(Vector3.up, direction);
                direction = Vector3.Dot(perp, (target - transform.position)) >= 0 ? perp : -perp;
            }

            Vector3 velocity = direction * speed;
            velocity.y = 0f; // Только по полу
            rb.linearVelocity = velocity;

            lastMoveDirection = direction;
            lookBackTimer = 0f;
            desiredYaw = YawOf(direction); // Взгляд доворачивается к направлению движения плавно
        }

        /// <summary>
        /// Установка направления взгляда
        /// </summary>
        private void SetLookDirection(Vector3 direction)
        {
            if (direction.sqrMagnitude < 0.0001f) return;

            lookDirection = new Vector2(direction.x, direction.z);
        }

        /// <summary>
        /// Обзор назад на точке патрулирования
        /// </summary>
        private void LookBackAtWaypoint()
        {
            // Плавный разворот на 180°; смотреть назад начинает, когда довернётся
            lookBackTimer = lookBackDuration;
            desiredYaw = facingYaw + 180f;
        }

        /// <summary>
        /// Подозрение: проверка пропа
        /// </summary>
        private void MaybeInspectProp()
        {
            if (playerDisguise == null || !playerDisguise.IsDisguised) return;
            if (!playerDisguise.IsMoving) return;
            if (propCheckTimer > 0f) return;

            float distance = FlatDistance(transform.position, playerTransform.position);
            if (distance > suspicionRange) return;

            propCheckTimer = propCheckCooldown;
            investigatePoint = playerTransform.position;
            state = EnemyState.Investigate;
        }

        /// <summary>
        /// Реакция на услышанный шум: идём проверять точку, если не в погоне
        /// </summary>
        public void HearNoise(Vector3 noisePosition)
        {
            if (state == EnemyState.Chase) return;

            investigatePoint = noisePosition;
            state = EnemyState.Investigate;
        }

        /// <summary>
        /// Назначение слоя стен (для врагов, созданных генератором)
        /// </summary>
        public void SetWallMask(LayerMask mask) => wallMask = mask;

        /// <summary>
        /// Дальность зрения × множитель (артефакты забега). Стражи создаются заново с каждым уровнем — эффект не копится
        /// </summary>
        public void ScaleVision(float multiplier) => visionRange *= multiplier;

        /// <summary>
        /// Настройка параметров для врагов, созданных из кода
        /// </summary>
        public void Configure(float visionRange, float fovDegrees, float patrolSpeed)
        {
            this.visionRange = visionRange;
            this.fovDegrees = Mathf.Clamp(fovDegrees, 10f, 120f);
            this.patrolSpeed = patrolSpeed;
        }

        /// <summary>
        /// Триггер тревоги
        /// </summary>
        public void TriggerAlert()
        {
            if (state != EnemyState.Chase)
            {
                state = EnemyState.Chase;
                noSightTimer = 0f;
                lastKnownPlayerPos = playerTransform != null ? playerTransform.position : transform.position;
            }
        }

        /// <summary>
        /// Вход в состояние поиска
        /// </summary>
        private void EnterSearch()
        {
            state = EnemyState.Search;
            searchTimer = searchDuration;
            searchSpinTimer = 0.5f;
        }

        /// <summary>
        /// Поиск ближайшей точки патрулирования
        /// </summary>
        private int FindNearestWaypoint()
        {
            if (WaypointCount == 0) return 0;

            int bestIndex = 0;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < WaypointCount; i++)
            {
                float distance = FlatDistance(transform.position, GetWaypoint(i));
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestIndex = i;
                }
            }

            return bestIndex;
        }

        /// <summary>
        /// Обновление визуала врага
        /// </summary>
        private void UpdateVisual()
        {
            // Плавный поворот взгляда с ограниченной скоростью; модель смотрит ровно туда же, куда конус
            float speed = state == EnemyState.Chase ? chaseTurnSpeed : turnSpeed;
            facingYaw = Mathf.MoveTowardsAngle(facingYaw, desiredYaw, speed * Time.deltaTime);
            SetLookDirection(ViewForward);

            if (visualTransform != null)
            {
                visualTransform.rotation = Quaternion.Euler(0f, facingYaw, 0f);
            }
        }

        /// <summary>
        /// Идёт ли игра. Без менеджера (отдельные тестовые сцены) страж работает всегда
        /// </summary>
        private static bool IsRunActive()
        {
            var gameManager = Core.GameManager3D.Instance;
            return gameManager == null
                || gameManager.CurrentState == Core.GameManager3D.GameState.Playing
                || gameManager.CurrentState == Core.GameManager3D.GameState.Intro;
        }

        /// <summary>
        /// Замечает ли страж игрока. Во вступлении уровня стражи уже ходят (игрок изучает маршруты),
        /// но никого не замечают — иначе ловили бы игрока до того, как ему дали управление
        /// </summary>
        private static bool IsDetectionActive()
        {
            var gameManager = Core.GameManager3D.Instance;
            return gameManager == null || gameManager.CurrentState == Core.GameManager3D.GameState.Playing;
        }

        /// <summary>
        /// Расстояние по полу: высота не должна влиять на патруль, погоню и поимку
        /// </summary>
        private static float FlatDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        /// <summary>
        /// Точка патруля в мире. В префабе куска точки хранятся в координатах куска:
        /// кусок ставится генератором в любое место уровня
        /// </summary>
        private int WaypointCount => route != null ? route.Count : waypoints.Count;

        private int NextWaypoint(int index) => route != null ? route.Next(index, ref routeDirection) : (index + 1) % waypoints.Count;

        private Vector3 GetWaypoint(int index)
        {
            if (route != null) return route.GetPoint(index);

            Vector3 point = waypoints[index];
            return waypointsInParentSpace && transform.parent != null ? transform.parent.TransformPoint(point) : point;
        }

        /// <summary>
        /// Добавление точки патрулирования
        /// </summary>
        public void AddWaypoint(Vector3 position)
        {
            waypoints.Add(position);
        }

        /// <summary>
        /// Автоматическое размещение точек патрулирования в комнате
        /// </summary>
        [ContextMenu("Auto Arrange Waypoints")]
        public void AutoArrangeWaypoints()
        {
            // Получение границ комнаты
            var colliders = GetComponentsInChildren<Collider>(true);
            if (colliders.Length == 0)
            {
                Debug.LogWarning("[EnemyAI3D] No colliders found for auto-arrangement.");
                return;
            }

            var minX = float.MaxValue;
            var maxX = float.MinValue;
            var minZ = float.MaxValue;
            var maxZ = float.MinValue;

            foreach (var collider in colliders)
            {
                if (!collider.isTrigger)
                {
                    var bounds = collider.bounds;
                    minX = Mathf.Min(minX, bounds.min.x);
                    maxX = Mathf.Max(maxX, bounds.max.x);
                    minZ = Mathf.Min(minZ, bounds.min.z);
                    maxZ = Mathf.Max(maxZ, bounds.max.z);
                }
            }

            // Отступ от стен
            var inset = 1f;
            var width = maxX - minX;
            var depth = maxZ - minZ;

            if (width < 2f || depth < 2f)
            {
                Debug.LogWarning("[EnemyAI3D] Room is too small for patrol.");
                return;
            }

            var halfW = width * 0.5f - inset;
            var halfD = depth * 0.5f - inset;
            var center = new Vector3((minX + maxX) * 0.5f, transform.position.y, (minZ + maxZ) * 0.5f);

            // Создаем 4 точки по углам комнаты
            waypoints = new List<Vector3>
            {
                new Vector3(center.x - halfW, center.y, center.z - halfD),
                new Vector3(center.x + halfW, center.y, center.z - halfD),
                new Vector3(center.x + halfW, center.y, center.z + halfD),
                new Vector3(center.x - halfW, center.y, center.z + halfD)
            };

            Debug.Log($"[EnemyAI3D] Arranged {waypoints.Count} waypoints.");
        }

        /// <summary>
        /// Тихая ликвидация (из маскировки)
        /// </summary>
        public void SilentKill()
        {
            state = EnemyState.Search;
            rb.linearVelocity = Vector3.zero;
            OnPlayerCaught = null;
            gameObject.SetActive(false);
        }

        /// <summary>
        /// Отладочная визуализация
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            // Точки патрулирования
            // Маршрут рисует себя сам (PatrolRoute3D); здесь — только запасной список точек
            Gizmos.color = Color.yellow;
            for (int i = 0; route == null && i < waypoints.Count; i++)
            {
                Gizmos.DrawSphere(GetWaypoint(i), 0.2f);
            }

            if (route == null && waypoints.Count > 1)
            {
                for (int i = 0; i < waypoints.Count; i++)
                {
                    int next = (i + 1) % waypoints.Count;
                    Gizmos.DrawLine(GetWaypoint(i), GetWaypoint(next));
                }
            }

            // Радиус зрения
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, visionRange);

            // Угол обзора
            if (lastMoveDirection.sqrMagnitude > 0.0001f)
            {
                Vector3 forward = ViewForward;
                Gizmos.color = Color.magenta;
                
                float halfFov = fovDegrees * 0.5f;
                Quaternion leftRay = Quaternion.AngleAxis(-halfFov, Vector3.up) * Quaternion.LookRotation(forward);
                Quaternion rightRay = Quaternion.AngleAxis(halfFov, Vector3.up) * Quaternion.LookRotation(forward);
                
                Gizmos.DrawRay(transform.position, leftRay * Vector3.forward * visionRange);
                Gizmos.DrawRay(transform.position, rightRay * Vector3.forward * visionRange);
            }
        }
    }
}