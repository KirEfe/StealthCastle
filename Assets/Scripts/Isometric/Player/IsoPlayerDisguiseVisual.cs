using UnityEngine;

namespace StealthCastle.Isometric.Player
{
    /// <summary>
    /// Визуал маскировки игрока (изометрия): при активации прячет основной спрайт вора
    /// и показывает дочерний рендерер с внешностью пропа. Адаптация Player.PlayerDisguiseVisual.
    /// </summary>
    public class IsoPlayerDisguiseVisual : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private SpriteRenderer mainSpriteRenderer;     // основной рендерер вора (на корне)
        [SerializeField] private SpriteRenderer disguiseSpriteRenderer;  // рендерер маскировки (дочерний, создаётся авто)

        private void Awake()
        {
            // Автопоиск, если не привязано в инспекторе
            if (mainSpriteRenderer == null) mainSpriteRenderer = GetComponent<SpriteRenderer>();
            if (disguiseSpriteRenderer == null) disguiseSpriteRenderer = CreateDisguiseRenderer();

            // Гарантируем, что дочерняя маскировка выключена при старте
            if (disguiseSpriteRenderer != null)
                disguiseSpriteRenderer.gameObject.SetActive(false);
        }

        // Создание дочернего объекта-маскировки (если его нет в префабе)
        private SpriteRenderer CreateDisguiseRenderer()
        {
            var go = new GameObject("DisguiseVisual");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.zero;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 5;
            go.SetActive(false);
            return sr;
        }

        /// <summary>Применить маскировку: спрятать вора, показать проп.</summary>
        public void ApplyDisguise(SpriteRenderer source)
        {
            if (source == null || disguiseSpriteRenderer == null) return;

            // 1. Отключаем визуал вора
            if (mainSpriteRenderer != null) mainSpriteRenderer.enabled = false;

            // 2. Включаем дочерний объект маскировки
            disguiseSpriteRenderer.gameObject.SetActive(true);

            // 3. Копируем внешность пропа
            disguiseSpriteRenderer.sprite = source.sprite;
            disguiseSpriteRenderer.color = source.color;
            disguiseSpriteRenderer.flipX = source.flipX;

            // 4. Центрируем локальную позицию
            disguiseSpriteRenderer.transform.localPosition = Vector3.zero;
        }

        /// <summary>Снять маскировку: вернуть вора.</summary>
        public void ClearDisguise()
        {
            if (disguiseSpriteRenderer != null)
            {
                disguiseSpriteRenderer.sprite = null;
                disguiseSpriteRenderer.gameObject.SetActive(false);
            }
            if (mainSpriteRenderer != null) mainSpriteRenderer.enabled = true;
        }
    }
}
