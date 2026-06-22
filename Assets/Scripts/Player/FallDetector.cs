using UnityEngine;
using System;
using StealthCastle.Player;
using StealthCastle.Core;

namespace StealthCastle.Player
{
    /// <summary>
    /// Компонент для определения высоты падения игрока и нанесения соответствующего урона или создания шума.
    /// </summary>
    public class FallDetector : MonoBehaviour
    {
        [Header("Пороги высоты падения")]
        [SerializeField] private float silentFallHeight = 2f; // До этой высоты падение бесшумное
        [SerializeField] private float loudFallHeight = 4f;    // Порог для среднего шума
        [SerializeField] private float damageHeight = 7f;     // Порог для нанесения урона

        [Header("Настройки урона")]
        [SerializeField] private float fallDamage = 20f;

        [Header("Настройки проверки земли")]
        [SerializeField] private float groundCheckDistance = 0.3f;
        [SerializeField] private LayerMask groundLayer;

        private HealthSystem healthSystem;
        private float jumpStartY;
        private bool isTrackingFall;

        /// <summary>
        /// Событие приземления: (высота падения, уровень шума)
        /// </summary>
        public event Action<float, NoiseLevel> OnFallLanded;

        public bool IsFalling => isTrackingFall;

        private void Awake()
        {
            healthSystem = GetComponent<HealthSystem>();
            if (healthSystem == null)
            {
                Debug.LogWarning($"[FallDetector] HealthSystem не найден на объекте {gameObject.name}. Урон от падения не будет наноситься.");
            }
        }

        private void Update()
        {
            CheckFall();
        }

        private void CheckFall()
        {
            // Проверка приземления с помощью Raycast вниз
            bool isGrounded = Physics2D.Raycast(transform.position, Vector2.down, groundCheckDistance, groundLayer);

            if (!isGrounded && !isTrackingFall)
            {
                // Игрок оторвался от земли — начинаем отслеживать высоту
                StartTrackingFall();
            }
            else if (isGrounded && isTrackingFall)
            {
                // Игрок приземлился — вычисляем разницу высот
                HandleLanding();
            }
        }

        private void StartTrackingFall()
        {
            jumpStartY = transform.position.y;
            isTrackingFall = true;
        }

        private void HandleLanding()
        {
            float fallHeight = jumpStartY - transform.position.y;
            isTrackingFall = false;

            if (fallHeight <= 0) return;

            ProcessFallResult(fallHeight);
        }

        private void ProcessFallResult(float height)
        {
            if (height <= silentFallHeight)
            {
                // Бесшумное падение
                return;
            }

            if (height <= loudFallHeight)
            {
                // Средний шум
                Debug.Log($"[FallDetector] Средний шум. Высота падения: {height:F2}");
                OnFallLanded?.Invoke(height, NoiseLevel.Medium);
            }
            else if (height <= damageHeight)
            {
                // Громкий шум
                Debug.Log($"[FallDetector] Громкий шум. Высота падения: {height:F2}");
                OnFallLanded?.Invoke(height, NoiseLevel.Loud);
            }
            else
            {
                // Громкий шум + урон
                Debug.Log($"[FallDetector] Громкий шум и урон! Высота падения: {height:F2}");
                
                if (healthSystem != null)
                {
                    healthSystem.TakeDamage(fallDamage);
                }

                OnFallLanded?.Invoke(height, NoiseLevel.Loud);
            }
        }
    }
}