using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using StealthCastle.Core;
using StealthCastle.Mechanics;

[RequireComponent(typeof(Rigidbody2D))]
public class GuardAI : MonoBehaviour
{
    [Header("Патруль")]
    [SerializeField] List<Transform> patrolPoints;
    [SerializeField] float walkSpeed = 2.5f;
    [SerializeField] float chaseSpeed = 4.5f;
    [SerializeField] float waitAtPointDuration = 1.5f;

    [Header("Поиск")]
    [SerializeField] float searchDuration = 5f;

    [Header("Зрение")]
    [SerializeField] float visionRange = 5f;
    [SerializeField] float proximityRange = 1f;
    [SerializeField] SpriteRenderer guardSprite;
    [SerializeField] LayerMask obstacleLayer;

    // Флаг перехода между этажами
    public bool IsTraversing { get; set; }
    // Свойство, чтобы другие скрипты знали, что мы в состоянии погони
    public bool IsChasing => currentState == GuardState.Chase;

    // Переменные для бега к двери
    private bool isChasingDoor = false;
    private Vector2 doorTargetPosition;

    enum GuardState { Patrol, Investigate, Chase, Search }
    GuardState currentState = GuardState.Patrol;

    int currentPatrolIndex = 0;
    Vector2 lastKnownPosition;
    Transform playerTransform;
    DisguiseSystem playerDisguise;

    bool isWaiting = false;
    float searchTimer;
    float flipTimer;
    Vector2 lastMoveDirection;
    
    private Rigidbody2D rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void Start()
    {
        GameObject playerGO = GameObject.FindGameObjectWithTag("Player");
        if (playerGO != null)
        {
            playerTransform = playerGO.transform;
            playerDisguise = playerGO.GetComponent<DisguiseSystem>();
        }

        NoiseEmitter.OnNoiseEmitted += HandleNoise;

        if (patrolPoints != null && patrolPoints.Count > 0)
        {
            currentState = GuardState.Patrol;
        }
    }

    void OnDestroy()
    {
        NoiseEmitter.OnNoiseEmitted -= HandleNoise;
    }


    public void SetChaseTargetDoor(Vector2 doorPos)
    {
        isChasingDoor = true;
        doorTargetPosition = doorPos;
    }

    public void ResetChaseTarget()
    {
        isChasingDoor = false;
    }

    public void LosePlayer()
    {
        lastKnownPosition = transform.position;
        ChangeState(GuardState.Search);
    }

    void Update()
    {
        // Если переходим между этажами — останавливаемся и отключаем логику
        if (IsTraversing)
        {
            rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
            return;
        }

        // Обнаружение игрока — только вне погони
        if (currentState != GuardState.Chase)
        {
            LookForPlayer();
        }

        ControlSpriteFlip();
    }

    void FixedUpdate()
    {
        if (IsTraversing) return;

        switch (currentState)
        {
            case GuardState.Patrol:     UpdatePatrol();     break;
            case GuardState.Investigate: UpdateInvestigate(); break;
            case GuardState.Chase:      UpdateChase();      break;
            case GuardState.Search:     UpdateSearch();     break;
        }
    }

    void UpdatePatrol()
    {
        if (patrolPoints == null || patrolPoints.Count == 0 || isWaiting) 
        {
            rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
            return;
        }

        Vector2 target = patrolPoints[currentPatrolIndex].position;
        float distToTarget = Mathf.Abs(target.x - transform.position.x);

        if (distToTarget < 0.1f && !isWaiting)
        {
            rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
            StartCoroutine(WaitAtPoint());
            return;
        }

        float dirX = Mathf.Sign(target.x - transform.position.x);
        lastMoveDirection = new Vector2(dirX, 0);
        rb.linearVelocity = new Vector2(dirX * walkSpeed, rb.linearVelocity.y);
    }

    IEnumerator WaitAtPoint()
    {
        isWaiting = true;
        yield return new WaitForSeconds(waitAtPointDuration);
        currentPatrolIndex = (currentPatrolIndex + 1) % patrolPoints.Count;
        isWaiting = false;
    }

    void UpdateInvestigate()
    {
        float distToTarget = Mathf.Abs(lastKnownPosition.x - transform.position.x);

        if (distToTarget < 0.2f)
        {
            rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
            ChangeState(GuardState.Search);
            return;
        }

        float dirX = Mathf.Sign(lastKnownPosition.x - transform.position.x);
        lastMoveDirection = new Vector2(dirX, 0);
        rb.linearVelocity = new Vector2(dirX * walkSpeed, rb.linearVelocity.y);
    }

    void UpdateChase()
    {
        if (playerTransform == null) return;

        // Если гонимся за дверью, цель — дверь. Если нет — сам игрок.
        Vector2 targetPos = isChasingDoor ? doorTargetPosition : (Vector2)playerTransform.position;

        // Если мы не бежим к двери, а игрок оказался на другом этаже (разница по высоте > 2.5) — мы его потеряли!
        if (!isChasingDoor && Mathf.Abs(playerTransform.position.y - transform.position.y) > 2.5f)
        {
            LosePlayer();
            return;
        }

        // Потеря из вида при маскировке
        if (!isChasingDoor && playerDisguise != null && playerDisguise.IsDisguised && !CanSeePlayer())
        {
            lastKnownPosition = playerTransform.position;
            ChangeState(GuardState.Search);
            return;
        }

        float distToTarget = Mathf.Abs(targetPos.x - transform.position.x);

        // Если добежали до двери, но нас еще не телепортировали — ждем на месте
        if (isChasingDoor && distToTarget < 0.1f)
        {
            rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
            return;
        }

        float dirX = Mathf.Sign(targetPos.x - transform.position.x);
        lastMoveDirection = new Vector2(dirX, 0);
        rb.linearVelocity = new Vector2(dirX * chaseSpeed, rb.linearVelocity.y);
        
        lastKnownPosition = targetPos;
    }

    void UpdateSearch()
    {
        // При поиске стоим на месте
        rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
        
        searchTimer -= Time.fixedDeltaTime;
        flipTimer -= Time.fixedDeltaTime;

        if (flipTimer <= 0f && guardSprite != null)
        {
            guardSprite.flipX = !guardSprite.flipX;
            flipTimer = 1.2f;
        }

        if (searchTimer <= 0f)
        {
            ChangeState(GuardState.Patrol);
        }
    }

    void LookForPlayer()
    {
        if (playerTransform == null) return;

        float dist = Vector2.Distance(transform.position, playerTransform.position);

        if (dist <= proximityRange)
        {
            TriggerAlert();
            return;
        }

        if (dist > visionRange) return;
        if (playerDisguise != null && playerDisguise.IsDisguised) return;

        float lookDir = (guardSprite != null && guardSprite.flipX) ? -1f : 1f;
        float dirToPlayer = playerTransform.position.x > transform.position.x ? 1f : -1f;

        if (lookDir != dirToPlayer) return;

        if (CanSeePlayer())
        {
            TriggerAlert();
        }
    }

    bool CanSeePlayer()
    {
        if (playerTransform == null) return false;

        Vector2 dir = (playerTransform.position - transform.position).normalized;
        float dist = Vector2.Distance(transform.position, playerTransform.position);

        RaycastHit2D hit = Physics2D.Raycast(transform.position, dir, dist, obstacleLayer);
        return hit.collider == null;
    }

    void HandleNoise(Vector2 noisePosition, float intensity)
    {
        if (currentState == GuardState.Chase || IsTraversing) return;

        if (Vector2.Distance(transform.position, noisePosition) <= intensity)
        {
            lastKnownPosition = noisePosition;
            isWaiting = false; // Сбрасываем ожидание, если услышал шум во время стоянки
            ChangeState(GuardState.Investigate);
        }
    }

    void TriggerAlert()
    {
        if (currentState != GuardState.Chase)
        {
            isWaiting = false;
            ChangeState(GuardState.Chase);
        }
    }

    void ChangeState(GuardState newState)
    {
        currentState = newState;
        if (newState == GuardState.Search)
        {
            searchTimer = searchDuration;
            flipTimer = 1.2f;
        }
    }

    void ControlSpriteFlip()
    {
        if (guardSprite == null || IsTraversing) return;

        if (lastMoveDirection.x > 0.01f)
            guardSprite.flipX = false;
        else if (lastMoveDirection.x < -0.01f)
            guardSprite.flipX = true; 
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, visionRange);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, proximityRange);

        // Линия направления взгляда
        float lookDir = (guardSprite != null && guardSprite.flipX) ? -1f : 1f;
        Gizmos.color = Color.cyan;
        Vector3 sightEnd = transform.position + (Vector3.right * lookDir * visionRange);
        Gizmos.DrawLine(transform.position, sightEnd);
    }
}