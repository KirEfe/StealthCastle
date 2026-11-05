using System.Collections.Generic;
using StealthCastle.Isometric.Mechanics;
using StealthCastle.Isometric.Stealth;
using UnityEngine;

namespace StealthCastle.Isometric.Enemies
{
    /// <summary>
    /// FSM врага (этап 3, изометрия): Patrol → Investigate → Chase → Search.
    /// Адаптация StealthCastle.Enemies.GuardAI + независимые мировые waypoints (как EnemyBase).
    /// Обнаружение через CanSeePlayer (raycast + маскировка). Поимка = конец забега.
    /// Реализует IVisionSource для EnemyVision (зрение отделено от мозгов).
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class IsoEnemyAI : MonoBehaviour, IVisionSource
    {
        private enum State { Patrol, Investigate, Chase, Search }

        [Header("Патруль")]
        [SerializeField] private Vector3[] waypoints;     // локальные точки (±5 в комнате 12)
        [SerializeField] private float patrolSpeed = 2f;
        [SerializeField] private float waypointReachDist = 0.3f;
        [SerializeField] private float waitAtWaypoint = 2f;   // ждёт на waypoint 2 сек

        [Header("Обнаружение")]
        [SerializeField] private float visionRange = 2.5f;       // радиус обнаружения 2.5 м
        [SerializeField, Range(10f, 120f)] private float fovDegrees = 45f; // угол обзора 45°
        [SerializeField] private float chaseSpeed = 4f;
        [SerializeField] private float crawlDetectDistance = 2f; // дистанция обнаружения ползущего
        [SerializeField] private float loseSightTime = 2f;
        [SerializeField] private LayerMask wallMask;            // слой стен для raycast

        [Header("Поиск")]
        [SerializeField] private float searchDuration = 4f;

        [Header("Подозрение")]
        [SerializeField] private float suspicionRange = 2.5f;   // радиус проверки пропа
        [SerializeField] private float propCheckCooldown = 3f;  // не чаще раза в 3 сек
        [SerializeField] private float lookBackDuration = 1f;   // обзор назад на повороте

        [Header("Ссылки")]
        [SerializeField] private SpriteRenderer guardSprite;    // визуал врага (flipX)

        private State state = State.Patrol;
        private Rigidbody2D rb;
        private int currentWaypoint;
        private float waitTimer;
        private float noSightTimer;
        private float searchTimer;
        private float propCheckTimer;   // кулдаун проверки пропа
        private float lookBackTimer;    // таймер обзора назад
        private float searchSpinTimer;  // таймер вращения в Search
        private Vector3 investigatePoint;
        private Vector3 lastKnownPlayerPos;
        private Vector3 lastMoveDirection;
        private Vector2 lookDirection;  // плавное направление взгляда (для EnemyVision)

        private IStealthTarget playerStealth;
        private Transform player;
        private IsoDisguiseSystem playerDisguise;

        public System.Action OnPlayerCaught; // событие поимки (для цикла роглайта)

        public Transform Player
        {
            get => player;
            set
            {
                player = value;
                if (player != null)
                {
                    playerStealth = player.GetComponent<IStealthTarget>();
                    playerDisguise = player.GetComponent<IsoDisguiseSystem>();
                }
            }
        }

        public LayerMask WallMask { get => wallMask; set => wallMask = value; }

        /// <summary>Патрульные точки (локальные, относительно корня куска). Редактируются в инспекторе.</summary>
        public IReadOnlyList<Vector3> PatrolWaypoints => waypoints;

        /// <summary>Авто-расстановка 4 точек по границам комнаты (через ContextMenu в инспекторе или редакторе).</summary>
        [ContextMenu("Расставить точки по комнате")]
        public void AutoArrangeWaypoints()
        {
            // Границы комнаты: ищем стены-потомки (BoxCollider2D на слое Wall) или пол.
            var colliders = GetComponentsInChildren<Collider2D>(true);
            if (colliders.Length == 0)
            {
                Debug.LogWarning("[IsoEnemyAI] Не найдено коллайдеров для авто-расстановки точек — комната пуста?", this);
                return;
            }

            var minX = float.MaxValue; var maxX = float.MinValue;
            var minY = float.MaxValue; var maxY = float.MinValue;
            foreach (var c in colliders)
            {
                if (!c.isTrigger && c is BoxCollider2D box)
                {
                    var b = box.bounds;
                    if (b.min.x < minX) minX = b.min.x;
                    if (b.max.x > maxX) maxX = b.max.x;
                    if (b.min.y < minY) minY = b.min.y;
                    if (b.max.y > maxY) maxY = b.max.y;
                }
            }

            // Отступ 1 м от стен, чтобы точка не застревала в коллайдере.
            var inset = 1f;
            var w = maxX - minX; var h = maxY - minY;
            if (w < 2f || h < 2f)
            {
                Debug.LogWarning("[IsoEnemyAI] Комната слишком мала для патруля.", this);
                return;
            }

            var halfW = w * 0.5f - inset; var halfH = h * 0.5f - inset;
            var center = new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);
            waypoints = new[]
            {
                new Vector3(center.x - halfW, center.y - halfH, 0),
                new Vector3(center.x + halfW, center.y - halfH, 0),
                new Vector3(center.x + halfW, center.y + halfH, 0),
                new Vector3(center.x - halfW, center.y + halfH, 0),
            };
            Debug.Log($"[IsoEnemyAI] Точки расставлены по комнате: {waypoints.Length} шт.", this);
        }

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;

            // Начальный взгляд врага — вправо (как было в старой логике)
            lookDirection = Vector2.right;

            if (player == null)
            {
                var go = GameObject.FindGameObjectWithTag("Player");
                if (go != null) Player = go.transform;
            }
        }

        private void Update()
        {
            if (player == null) return;

            // Декремент таймеров подозрения/обзора
            if (propCheckTimer > 0) propCheckTimer -= Time.deltaTime;
            if (lookBackTimer > 0) lookBackTimer -= Time.deltaTime;
            if (searchSpinTimer > 0) searchSpinTimer -= Time.deltaTime;

            if (state != State.Chase) LookForPlayer();
            ControlSpriteFlip();
        }

        private void FixedUpdate()
        {
            if (player == null) return;

            switch (state)
            {
                case State.Patrol: UpdatePatrol(); break;
                case State.Investigate: UpdateInvestigate(); break;
                case State.Chase: UpdateChase(); break;
                case State.Search: UpdateSearch(); break;
            }
        }

        private void LookForPlayer()
        {
            var dist = Vector2.Distance(transform.position, player.position);

            // Ползущий замечен только вблизи
            if (playerDisguise != null && playerDisguise.IsDisguised && playerDisguise.IsMoving)
            {
                if (dist <= crawlDetectDistance) { TriggerAlert(); return; }
            }

            // Неподвижный замаскированный — невидим
            if (playerStealth != null && !playerStealth.CanBeDetected()) return;

            if (dist > visionRange) return;
            if (CanSeePlayer()) TriggerAlert();
        }

        private bool CanSeePlayer()
        {
            var toPlayer = (Vector2)(player.position - transform.position);
            var dist = toPlayer.magnitude;
            if (dist > visionRange) return false;

            // Проверка угла обзора (относительно взгляда врага; при обзоре назад — инверсия)
            var baseForward = lastMoveDirection.sqrMagnitude > 0.0001f ? (Vector2)lastMoveDirection : Vector2.right;
            var forward = lookBackTimer > 0f ? -baseForward : baseForward;
            var angle = Vector2.Angle(forward, toPlayer);
            if (angle > fovDegrees * 0.5f) return false;

            var hit = Physics2D.Raycast(transform.position, toPlayer.normalized, dist, wallMask);
            return hit.collider == null;
        }

        private void UpdatePatrol()
        {
            if (waypoints == null || waypoints.Length == 0)
            {
                rb.linearVelocity = Vector2.zero;
                return;
            }

            var target = WorldWaypoint(currentWaypoint);
            if (Vector2.Distance(transform.position, target) < waypointReachDist)
            {
                rb.linearVelocity = Vector2.zero;
                waitTimer += Time.fixedDeltaTime;
                if (waitTimer >= waitAtWaypoint)
                {
                    waitTimer = 0f;
                    LookBackAtWaypoint();   // обзор назад перед сменой направления
                    currentWaypoint = (currentWaypoint + 1) % waypoints.Length;
                }
                return;
            }

            MaybeInspectProp();  // подозрение к ползущему в маскировке
            MoveTowards(target, patrolSpeed);
            SetLookDirection(lastMoveDirection);   // зрение смотрит по движению
        }

        private void UpdateInvestigate()
        {
            var target = WorldWaypoint(-1, investigatePoint);
            if (Vector2.Distance(transform.position, target) < waypointReachDist)
            {
                rb.linearVelocity = Vector2.zero;
                EnterSearch();
                return;
            }
            MoveTowards(target, patrolSpeed);
            SetLookDirection(lastMoveDirection);   // зрение смотрит по движению
        }

        // Локальный waypoint → мировая точка (относительно root-куска врага)
        private Vector3 WorldWaypoint(int index, Vector3 fallback = default)
        {
            if (index >= 0 && waypoints != null && index < waypoints.Length)
            {
                var local = waypoints[index];
                var root = transform.parent != null ? transform.parent : transform;
                return root.TransformPoint(local);
            }
            return fallback;
        }

        private void UpdateChase()
        {
            if (player == null) return;

            if (playerDisguise != null && playerDisguise.IsDisguised && !CanSeePlayer())
            {
                lastKnownPlayerPos = player.position;
                EnterSearch();
                return;
            }

            if (CanSeePlayer())
                lastKnownPlayerPos = player.position;

            MoveTowards(lastKnownPlayerPos, chaseSpeed);

            if (Vector2.Distance(transform.position, player.position) < 0.6f)
            {
                // Защита: поимка только во время активного забега (иначе стреляет после смерти)
                if (Core.RunManager.Instance != null && Core.RunManager.Instance.Phase == Core.RunPhase.Playing)
                    OnPlayerCaught?.Invoke();
                return;
            }

            noSightTimer += Time.fixedDeltaTime;
            if (noSightTimer >= loseSightTime) EnterSearch();
        }

        private void UpdateSearch()
        {
            rb.linearVelocity = Vector2.zero;

            // Крутимся на месте, имитируя осмотр (меняем направление взгляда по кругу)
            if (searchSpinTimer <= 0f)
            {
                searchSpinTimer = 0.5f;
                // поворот взгляда на 90°: (x,y) -> (-y, x)
                var d = (Vector2)lastMoveDirection;
                lastMoveDirection = d.sqrMagnitude < 0.0001f ? Vector3.right : new Vector3(-d.y, d.x, 0f);
                SetLookDirection(lastMoveDirection);   // зрение поворачивается при осмотре
                if (guardSprite != null) guardSprite.flipX = lastMoveDirection.x < 0f;
            }

            searchTimer -= Time.fixedDeltaTime;
            if (searchTimer <= 0f)
            {
                state = State.Patrol;
                if (waypoints != null && waypoints.Length > 0)
                    currentWaypoint = NearestWaypoint();
            }
        }

        private int NearestWaypoint()
        {
            var best = 0;
            var bestDist = float.MaxValue;
            for (var i = 0; i < waypoints.Length; i++)
            {
                var d = Vector2.Distance(transform.position, WorldWaypoint(i));
                if (d < bestDist) { bestDist = d; best = i; }
            }
            return best;
        }

        // Обзор назад на повороте waypoint: разворачиваем взгляд на ~1 сек
        private void LookBackAtWaypoint()
        {
            if (lastMoveDirection.sqrMagnitude < 0.0001f) return;
            lookBackTimer = lookBackDuration;
            var back = -lastMoveDirection;          // смотрим назад
            SetLookDirection(back);                 // зрение разворачивается назад
            if (guardSprite != null) guardSprite.flipX = back.x < 0f;
        }

        // Подозрение: если игрок полз к врагу в маскировке — идём проверить проп
        private void MaybeInspectProp()
        {
            if (playerDisguise == null || !playerDisguise.IsDisguised) return;
            if (!playerDisguise.IsMoving) return;   // неподвижный проп — не проверяем (честно)
            if (propCheckTimer > 0f) return;
            var dist = Vector2.Distance(transform.position, player.position);
            if (dist > suspicionRange) return;

            propCheckTimer = propCheckCooldown;
            // Переход в Investigate: идём проверить точку у игрока
            state = State.Investigate;
            investigatePoint = player.position;
        }

        private void MoveTowards(Vector3 target, float speed)
        {
            var dir = (Vector2)(target - transform.position);
            if (dir.sqrMagnitude < 0.0001f) return; // уже на точке
            dir.Normalize();

            // Обход стен: если впереди стена — скользим вдоль
            var hit = Physics2D.Raycast(transform.position, dir, 0.8f, wallMask);
            if (hit.collider != null)
            {
                var perp = new Vector2(-dir.y, dir.x);
                dir = Vector2.Dot(perp, (Vector2)(target - transform.position)) >= 0 ? perp : -perp;
            }

            rb.linearVelocity = dir * speed;
            lastMoveDirection = dir;
            lookBackTimer = 0f;   // при движении смотрим вперёд, обзор назад сброшен

            SetLookDirection(dir);                // зрение смотрит по движению
            if (guardSprite != null) guardSprite.flipX = dir.x < 0f;   // визуал врага
        }

        public void TriggerAlert()
        {
            if (state != State.Chase)
            {
                state = State.Chase;
                noSightTimer = 0f;
                lastKnownPlayerPos = player.position;
            }
        }

        private void EnterSearch()
        {
            state = State.Search;
            searchTimer = searchDuration;
        }

        public bool CanSeePlayerNow()
        {
            if (player == null) return false;
            // Неподвижный замаскированный — невидим (Вариант А)
            if (playerDisguise != null && playerDisguise.IsDisguised && !playerDisguise.IsMoving)
                return false;
            return CanSeePlayer();
        }

        #region IVisionSource

        // Куда смотрит враг прямо сейчас — EnemyVision плавно повернёт зону
        public Vector2 LookDirection => lookDirection;
        public float VisionRange => visionRange;
        public float FovDegrees => fovDegrees;

        // Обновить направление взгляда (вызывается из всех стейтов при смене направления)
        private void SetLookDirection(Vector2 dir)
        {
            if (dir.sqrMagnitude < 0.0001f) return;
            lookDirection = dir;
        }

        #endregion

        // Тихое убийство из маскировки/со спины. НЕ триггерит OnPlayerCaught (иначе игрок умрёт).
        public void SilentKill()
        {
            state = State.Search;
            rb.linearVelocity = Vector2.zero;
            OnPlayerCaught = null; // критично: не убиваем игрока вместе с врагом
            gameObject.SetActive(false);
        }

        private void ControlSpriteFlip()
        {
            if (guardSprite == null) return;
            if (lookBackTimer > 0f) return; // не перебиваем обзор назад на waypoint

            if (lastMoveDirection.x > 0.01f) guardSprite.flipX = false;
            else if (lastMoveDirection.x < -0.01f) guardSprite.flipX = true;
        }

        #region Editor Gizmos
        // Отрисовка маршрута патруля в редакторе (видно без кастомного инспектора).
        private void OnDrawGizmosSelected()
        {
            if (waypoints == null || waypoints.Length < 2) return;

            var root = transform.parent != null ? transform.parent : transform;
            Gizmos.color = new Color(1f, 0.85f, 0f, 0.9f);
            Gizmos.matrix = Matrix4x4.TRS(root.position, root.rotation, root.lossyScale);

            for (var i = 0; i < waypoints.Length; i++)
            {
                var next = (i + 1) % waypoints.Length;
                // Точка-маркер
                Gizmos.DrawSphere(waypoints[i], 0.15f);
                // Линия маршрута
                Gizmos.DrawLine(waypoints[i], waypoints[next]);
            }

            // Первая точка — зелёная (старт патруля)
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(waypoints[0], 0.2f);
        }
        #endregion
    }
}