using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(BoxCollider2D))]
public class FloorTransition : MonoBehaviour
{
    [Header("Exit Points")]
    [SerializeField] private Transform upExit;
    [SerializeField] private Transform downExit;

    [Header("Settings")]
    [SerializeField] private bool hasUpExit = true;
    [SerializeField] private bool hasDownExit = true;

    [Header("Fade Settings")]
    [SerializeField] private float fadeOutDuration = 0.3f;
    [SerializeField] private float fadeInDuration = 0.3f;
    [SerializeField] private GameObject fadePrefab;

    [Header("Enemy Tracking (Хлебные крошки)")]
    [SerializeField] private float enemyTransitionDelay = 1f; // Задержка стражника на лестнице
    [SerializeField] private float breadcrumbDuration = 4f;   // Сколько секунд стражник может "взять след"

    private const float verticalThreshold = 0.5f;

    private GameObject playerObject;
    private PlayerController playerController;
    
    private bool isPlayerTransitioning = false;
    
    // Память для стражников
    private Transform lastPlayerExit;
    private float breadcrumbTimer = 0f;

    // СТАТИЧЕСКИЕ ПЕРЕМЕННЫЕ
    private static GameObject sharedFadeGO;
    private static Image sharedFadeImage;

    private void Awake()
    {
        GetComponent<BoxCollider2D>().isTrigger = true;
    }

    private void Start()
    {
        if (fadePrefab != null && sharedFadeGO == null)
        {
            sharedFadeGO = Instantiate(fadePrefab);
            sharedFadeImage = sharedFadeGO.GetComponentInChildren<Image>();
            
            if (sharedFadeImage != null)
            {
                sharedFadeImage.color = new Color(0f, 0f, 0f, 0f);
            }
            sharedFadeGO.SetActive(false);
            DontDestroyOnLoad(sharedFadeGO); 
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // 1. Обработка ИГРОКА
        if (other.CompareTag("Player") && !isPlayerTransitioning)
        {
            playerObject = other.gameObject;
            playerController = other.GetComponent<PlayerController>();
        }
        
        // 2. Обработка СТРАЖНИКОВ
        else if (other.CompareTag("Enemy"))
        {
            GuardAI guard = other.GetComponent<GuardAI>();
            
            if (guard != null && !guard.IsTraversing && guard.IsChasing)
            {
                if (lastPlayerExit != null)
                {
                    StartCoroutine(EnemyTransitionSequence(other.gameObject, guard, lastPlayerExit));
                }
                else
                {
                    // След простыл! Игрок ушел слишком давно
                    guard.ResetChaseTarget();
                    guard.LosePlayer();
                }
            }
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            if (isPlayerTransitioning) return;
            playerObject = null;
            playerController = null;
        }
    }

    private void Update()
    {
        // Таймер исчезновения "следа" игрока
        if (breadcrumbTimer > 0f)
        {
            breadcrumbTimer -= Time.deltaTime;
            if (breadcrumbTimer <= 0f)
            {
                lastPlayerExit = null; // След простыл
            }
        }

        if (isPlayerTransitioning || playerObject == null || playerController == null) return;

        Vector2 input = playerController.MoveInput;

        if (input.y > verticalThreshold && hasUpExit && upExit != null)
        {
            StartPlayerTransition(upExit);
        }
        else if (input.y < -verticalThreshold && hasDownExit && downExit != null)
        {
            StartPlayerTransition(downExit);
        }
    }

    private void StartPlayerTransition(Transform exit)
    {
        // ОСТАВЛЯЕМ СЛЕД: Запоминаем, куда ушел игрок, и обновляем таймер
        lastPlayerExit = exit;
        breadcrumbTimer = breadcrumbDuration;

        // Говорим всем стражникам на ЭТОМ этаже, которые в погоне, бежать к этой двери
        GuardAI[] guards = FindObjectsByType<GuardAI>(FindObjectsSortMode.None);
        foreach (var guard in guards)
        {
            // Проверяем, что стражник в погоне и находится примерно на той же высоте (на том же этаже)
            if (guard.IsChasing && Mathf.Abs(guard.transform.position.y - transform.position.y) < 2f)
            {
                guard.SetChaseTargetDoor(transform.position);
            }
        }

        if (sharedFadeGO == null || sharedFadeImage == null)
        {
            playerObject.transform.position = exit.position;
            return;
        }

        StartCoroutine(PlayerTransitionSequence(exit));
    }

    // ---------- КОРУТИНА ИГРОКА ----------
    private IEnumerator PlayerTransitionSequence(Transform exit)
    {
        isPlayerTransitioning = true;
        playerController.SetInputEnabled(false);

        sharedFadeGO.SetActive(true);
        yield return StartCoroutine(FadeValue(0f, 1f, fadeOutDuration));

        Rigidbody2D rb = playerObject.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }

        playerObject.transform.position = exit.position;
        yield return new WaitForEndOfFrame();

        yield return StartCoroutine(FadeValue(1f, 0f, fadeInDuration));
        sharedFadeGO.SetActive(false);

        if (playerController != null)
        {
            playerController.SetInputEnabled(true);
        }

        playerObject = null;
        playerController = null;
        isPlayerTransitioning = false;
    }

    // ---------- КОРУТИНА СТРАЖНИКА ----------
    private IEnumerator EnemyTransitionSequence(GameObject enemyGO, GuardAI guard, Transform exit)
    {
        
        // Замораживаем ТОЛЬКО этого конкретного стражника
        guard.IsTraversing = true;
        
        // Ждем, пока он "поднимается/спускается"
        yield return new WaitForSeconds(enemyTransitionDelay);
        
        Rigidbody2D rb = enemyGO.GetComponent<Rigidbody2D>();
        if (rb != null) rb.linearVelocity = Vector2.zero;
        
        // Перемещаем его на тот же выход, куда ушел игрок
        enemyGO.transform.position = exit.position;
        
        yield return new WaitForEndOfFrame();
        
        // Размораживаем и говорим снова бежать за игроком
        guard.ResetChaseTarget();
        // Размораживаем
        guard.IsTraversing = false;
    }

    private IEnumerator FadeValue(float startAlpha, float endAlpha, float duration)
    {
        float elapsed = 0f;
        Color color = sharedFadeImage.color;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            color.a = Mathf.Lerp(startAlpha, endAlpha, t);
            sharedFadeImage.color = color;
            yield return null;
        }

        color.a = endAlpha;
        sharedFadeImage.color = color;
    }
}