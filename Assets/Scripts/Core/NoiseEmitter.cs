using UnityEngine;
using System;
using System.Collections.Generic;

namespace StealthCastle.Core
{
    /// <summary>
    /// Уровни шума, которые может издавать игрок
    /// </summary>
    public enum NoiseLevel
    {
        None,   // Без шума
        Soft,   // Тихий шум (радиус 1)
        Medium, // Средний шум (радиус 3)
        Loud    // Громкий шум (радиус 6)
    }

    /// <summary>
    /// Единый компонент шума. Все источники шума используют его.
    /// Вешается на Player.
    /// </summary>
    public class NoiseEmitter : MonoBehaviour
    {
        [Header("Радиусы шума")]
        [SerializeField] private float softNoiseRadius = 1f;
        [SerializeField] private float mediumNoiseRadius = 3f;
        [SerializeField] private float loudNoiseRadius = 6f;

        [Header("Отладка")]
        [SerializeField] private bool drawGizmos = true;

        public static event Action<Vector2, float> OnNoiseEmitted;

        /// <summary>
        /// Издаёт шум указанного уровня. Враги с EnemyHearing услышат его.
        /// </summary>
        /// <param name="level">Уровень шума</param>
        public void Emit(NoiseLevel level)
        {
            float radius = GetRadius(level);
            OnNoiseEmitted?.Invoke(transform.position, radius);
            
            if (radius <= 0f) return;

            // Поиск всех коллайдеров в радиусе
            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, radius);
            
            foreach (Collider2D hit in hits)
            {
                // Попытка найти EnemyHearing на объекте
                // В данный момент EnemyHearing не существует, поэтому используем заглушку
                if (hit.gameObject != null)
                {
                    // Заглушка: EnemyHearing ещё не реализован
                    Debug.Log($"[NoiseEmitter] Враг {hit.name} слышит шум: {level} (радиус: {radius})");
                }
            }

            // Визуализация в редакторе (только для отладки в режиме реального времени)
            if (drawGizmos)
            {
                // Примечание: Gizmos.DrawWireSphere работает только в OnDrawGizmos, 
                // здесь мы просто логируем факт срабатывания.
            }
        }

        /// <summary>
        /// Возвращает радиус шума для указанного уровня
        /// </summary>
        private float GetRadius(NoiseLevel level)
        {
            return level switch
            {
                NoiseLevel.Soft => softNoiseRadius,
                NoiseLevel.Medium => mediumNoiseRadius,
                NoiseLevel.Loud => loudNoiseRadius,
                _ => 0f
            };
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawGizmos) return;

            // Отрисовка всех уровней шума для наглядности при выделении объекта
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, softNoiseRadius);

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, mediumNoiseRadius);

            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, loudNoiseRadius);
        }
    }
}