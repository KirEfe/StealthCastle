using UnityEngine;

namespace StealthCastle.Player
{
    // Убираем RequireComponent(typeof(SpriteRenderer)), так как теперь у нас два рендерера
    public class PlayerDisguiseVisual : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private SpriteRenderer mainSpriteRenderer;   // Основной рендерер вора (на корневом объекте)
        [SerializeField] private Animator playerAnimator;             // Аниматор вора (на корневом объекте)
        [SerializeField] private SpriteRenderer disguiseSpriteRenderer; // Рендерер маскировки (на ДОЧЕРНЕМ объекте)

        void Awake()
        {
            // Автоматический поиск компонентов на случай, если забыли привязать в инспекторе
            if (mainSpriteRenderer == null) mainSpriteRenderer = GetComponent<SpriteRenderer>();
            if (playerAnimator == null) playerAnimator = GetComponent<Animator>();
            
            // Гарантируем, что при старте игры дочерняя маскировка выключена
            if (disguiseSpriteRenderer != null)
            {
                disguiseSpriteRenderer.gameObject.SetActive(false);
            }
        }

        public void ApplyDisguise(SpriteRenderer source)
        {
            if (source == null || disguiseSpriteRenderer == null)
                return;

            // 1. ПОЛНОСТЬЮ ОТКЛЮЧАЕМ визуал и аниматор вора, чтобы они не мешали
            if (mainSpriteRenderer != null) mainSpriteRenderer.enabled = false;
            if (playerAnimator != null) playerAnimator.enabled = false;

            // 2. Включаем дочерний объект маскировки
            disguiseSpriteRenderer.gameObject.SetActive(true);

            // 3. Копируем в него внешность предмета
            disguiseSpriteRenderer.sprite = source.sprite;
            disguiseSpriteRenderer.color = source.color;
            disguiseSpriteRenderer.flipX = source.flipX;

            // 4. Сбрасываем локальную позицию дочернего объекта строго в центр (0,0,0)
            disguiseSpriteRenderer.transform.localPosition = Vector3.zero;
        }

        public void ClearDisguise()
        {
            // 1. Выключаем и очищаем дочернюю маскировку
            if (disguiseSpriteRenderer != null)
            {
                disguiseSpriteRenderer.sprite = null;
                disguiseSpriteRenderer.gameObject.SetActive(false);
            }

            // 2. Возвращаем обратно родной визуал и аниматор вора
            if (mainSpriteRenderer != null) mainSpriteRenderer.enabled = true;
            if (playerAnimator != null) playerAnimator.enabled = true;
        }
    }
}