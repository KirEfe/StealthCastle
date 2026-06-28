using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using StealthCastle.Core;
using StealthCastle.Mechanics;

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

    // Флаг перехода между этажами — устанавливает EnemyFloorTransition
    public bool IsTraversing { get; set; }

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

    void Start()
    {
        // Находим игрока по тегу
        GameObject playerGO = GameObject.FindGameObjectWithTag("Player");
        if (playerGO != null)
        {
            playerTransform = playerGO.transform;
            playerDisguise = playerGO.GetComponent<DisguiseSystem>();
        }

        // Подписываемся на событие шума
        NoiseEmitter.OnNoiseEmitted += HandleNoise;

        if (patrolPoints != null && patrolPoints.Count > 0)
        {
            currentState = GuardState.Patrol;
        }
    }

    void OnDestroy()
    {
        // Отписываемся при уничтожении объекта
        NoiseEmitter.OnNoiseEmitted -= HandleNoise;
    }

    void Update()
    {
        // Стражник заморожен во время перехода между этажами
        if (IsTraversing) return;

        switch (currentState)
        {
            case GuardState.Patrol:     UpdatePatrol();     break;
            case GuardState.Investigate: UpdateInvestigate(); break;
            case GuardState.Chase:      UpdateChase();      break;
            case GuardState.Search:     UpdateSearch();     break;
        }

        // Обнаружение игрока — только вне погони
        if (currentState != GuardState.Chase)
        {
            LookForPlayer();
        }

        ControlSpriteFlip();
    }

    void UpdatePatrol()
    {
        if (patrolPoints == null || patrolPoints.Count == 0 || isWaiting) return;

        Vector2 target = patrolPoints[currentPatrolIndex].position;
        lastMoveDirection = (target - (Vector2)transform.position).normalized;

        transform.position = Vector2.MoveTowards(
            transform.position, target, walkSpeed * Time.deltaTime);

        if (Vector2.Distance(transform.position, target) < 0.1f && !isWaiting)
        {
            StartCoroutine(WaitAtPoint());
        }
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
        lastMoveDirection = (lastKnownPosition - (Vector2)transform.position).normalized;

        transform.position = Vector2.MoveTowards(
            transform.position, lastKnownPosition, walkSpeed * Time.deltaTime);

        if (Vector2.Distance(transform.position, lastKnownPosition) < 0.2f)
        {
            ChangeState(GuardState.Search);
        }
    }

    void UpdateChase()
    {
        if (playerTransform == null) return;

        // Если игрок замаскировался и мы не видим его напрямую — переходим к поиску
        if (playerDisguise != null && playerDisguise.IsDisguised && !CanSeePlayer())
        {
            lastKnownPosition = playerTransform.position;
            ChangeState(GuardState.Search);
            return;
        }

        lastMoveDirection = ((Vector2)playerTransform.position - (Vector2)transform.position).normalized;

        transform.position = Vector2.MoveTowards(
            transform.position, playerTransform.position, chaseSpeed * Time.deltaTime);

        lastKnownPosition = playerTransform.position;
    }

    void UpdateSearch()
    {
        searchTimer -= Time.deltaTime;
        flipTimer -= Time.deltaTime;

        // Крутим головой по сторонам каждые 1.2 сек
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

        // Зона близости — шестое чувство, работает даже при маскировке
        if (dist <= proximityRange)
        {
            TriggerAlert();
            return;
        }

        // Конус зрения — не работает при маскировке
        if (dist > visionRange) return;
        if (playerDisguise != null && playerDisguise.IsDisguised) return;

        // Проверяем направление взгляда стражника
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

        // Raycast проверяет нет ли препятствия между стражником и игроком
        RaycastHit2D hit = Physics2D.Raycast(transform.position, dir, dist, obstacleLayer);

        return hit.collider == null;
    }

    void HandleNoise(Vector2 noisePosition, float intensity)
    {
        // Игнорируем шум во время погони или перехода
        if (currentState == GuardState.Chase || IsTraversing) return;

        if (Vector2.Distance(transform.position, noisePosition) <= intensity)
        {
            lastKnownPosition = noisePosition;
            ChangeState(GuardState.Investigate);
        }
    }

    void TriggerAlert()
    {
        if (currentState != GuardState.Chase)
        {
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
            guardSprite.flipX = false; // смотрит вправо
        else if (lastMoveDirection.x < -0.01f)
            guardSprite.flipX = true;  // смотрит влево
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, visionRange);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, proximityRange);
    }
}
