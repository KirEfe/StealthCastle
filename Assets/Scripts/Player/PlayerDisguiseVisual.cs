using UnityEngine;
using StealthCastle;

namespace StealthCastle.Player
{
    [RequireComponent(typeof(SpriteRenderer))]
    public class PlayerDisguiseVisual : MonoBehaviour
    {
        // Компонент спрайта игрока
        private SpriteRenderer spriteRenderer;

        // Сохранённые оригинальные параметры спрайта
        private Sprite originalSprite;
        private Color originalColor;
        private bool originalFlipX;
        private Vector3 originalLocalPosition;

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            CacheOriginalAppearance();
        }

        /// <summary>
        /// Сохраняет текущий внешний вид игрока, чтобы можно было вернуть его после маскировки.
        /// </summary>
        private void CacheOriginalAppearance()
        {
            originalSprite = spriteRenderer.sprite;
            originalColor = spriteRenderer.color;
            originalFlipX = spriteRenderer.flipX;
            originalLocalPosition = transform.localPosition;
        }

        /// <summary>
        /// Применяет спрайт маскировки и выравнивает его по нижней границе коллайдера игрока.
        /// </summary>
        /// <param name="source">SpriteRenderer объекта, из которого берём спрайт и параметры.</param>
        public void ApplyDisguise(SpriteRenderer source)
        {
            if (source == null)
                return;

            // Просто копируем картинку. Физический коллайдер теперь сам встанет как надо, 
            // а гравитация прижмет объект к полу.
            spriteRenderer.sprite = source.sprite;
            spriteRenderer.color = source.color;
            spriteRenderer.flipX = source.flipX;
        }
        /// <summary>
        /// Возвращает оригинальный внешний вид игрока.
        /// </summary>
        public void ClearDisguise()
        {
            spriteRenderer.sprite = originalSprite;
            spriteRenderer.color = originalColor;
            spriteRenderer.flipX = originalFlipX;
            
            // // Убеждаемся, что локальная позиция визуала всегда сброшена в ноль
            // transform.localPosition = originalLocalPosition;
        }
        /// <summary>
        /// Выравнивает спрайт по нижней границе коллайдера игрока.
        /// </summary>
    private void AlignToCollider()
    {
        var capsule = GetComponent<CapsuleCollider2D>();
        if (capsule == null) return;

        // Сначала сбрасываем смещение чтобы bounds был чистым
        spriteRenderer.transform.localPosition = originalLocalPosition;

        // Нижняя точка коллайдера в мировых координатах
        float colliderBottom = transform.position.y 
            + capsule.offset.y 
            - capsule.size.y / 2f;

        // Нижняя граница спрайта в мировых координатах
        float spriteBottom = spriteRenderer.bounds.min.y;

        // Смещение для выравнивания
        float deltaY = colliderBottom - spriteBottom;

        // Применяем в локальных координатах
        var localPos = spriteRenderer.transform.localPosition;
        localPos.y += deltaY;
        spriteRenderer.transform.localPosition = localPos;
    }
    }
}