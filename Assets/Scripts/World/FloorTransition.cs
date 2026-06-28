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

    private const float verticalThreshold = 0.5f;

    private GameObject playerObject;
    private PlayerController playerController;
    private bool isTransitioning = false;

    // СТАТИЧЕСКИЕ ПЕРЕМЕННЫЕ: будут общими для ВСЕХ переходов на сцене
    private static GameObject sharedFadeGO;
    private static Image sharedFadeImage;

    private void Awake()
    {
        BoxCollider2D col = GetComponent<BoxCollider2D>();
        col.isTrigger = true;
    }

    private void Start()
    {
        // Инициализируем префаб ТОЛЬКО ОДИН РАЗ, даже если лестниц на сцене сотня
        if (fadePrefab != null && sharedFadeGO == null)
        {
            sharedFadeGO = Instantiate(fadePrefab);
            // Ищем Image в том числе на дочерних объектах префаба Canvas
            sharedFadeImage = sharedFadeGO.GetComponentInChildren<Image>();
            
            if (sharedFadeImage != null)
            {
                sharedFadeImage.color = new Color(0f, 0f, 0f, 0f);
            }
            sharedFadeGO.SetActive(false);
            
            // Защищаем от удаления при переходе между комнатами/сценами
            DontDestroyOnLoad(sharedFadeGO); 
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isTransitioning) return;

        if (other.CompareTag("Player"))
        {
            playerObject = other.gameObject;
            playerController = other.GetComponent<PlayerController>();
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            // ИСПРАВЛЕНИЕ: Если прямо сейчас идет процесс телепортации, 
            // мы ИГНОРИРУЕМ выход из триггера, так как он вызван самим перемещением!
            if (isTransitioning) return;

            playerObject = null;
            playerController = null;
        }
    }

    private void Update()
    {
        // Проверяем наличие компонентов
        if (isTransitioning || playerObject == null || playerController == null) return;

        // Безопасная проверка ввода (с учетом возможных изменений в PlayerController)
        Vector2 input = playerController.MoveInput;

        if (input.y > verticalThreshold && hasUpExit && upExit != null)
        {
            StartTransition(upExit);
        }
        else if (input.y < -verticalThreshold && hasDownExit && downExit != null)
        {
            StartTransition(downExit);
        }
    }

    private void StartTransition(Transform exit)
    {
        if (sharedFadeGO == null || sharedFadeImage == null)
        {
            // Если префаб интерфейса забыли настроить, переносим физически без анимации
            playerObject.transform.position = exit.position;
            return;
        }

        StartCoroutine(TransitionSequence(exit));
    }

    private IEnumerator TransitionSequence(Transform exit)
    {
        isTransitioning = true;

        // Блокируем ввод вора
        playerController.SetInputEnabled(false);

        // Включаем общий холст и делаем затемнение
        sharedFadeGO.SetActive(true);
        yield return StartCoroutine(FadeValue(0f, 1f, fadeOutDuration));

        // Мгновенный телепорт на целевую точку
        playerObject.transform.position = exit.position;

        // Даем Unity один кадр на обновление физического положения игрока на новом этаже
        yield return new WaitForEndOfFrame();

        // Плавно осветляем экран обратно
        yield return StartCoroutine(FadeValue(1f, 0f, fadeInDuration));
        sharedFadeGO.SetActive(false);

        // Разблокируем ввод игрока
        if (playerController != null)
        {
            playerController.SetInputEnabled(true);
        }

        // Вручную очищаем ссылки, так как игрок физически покинул этот этаж
        playerObject = null;
        playerController = null;
        isTransitioning = false;
    }

    // Универсальный метод плавного изменения прозрачности (работает в обе стороны)
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