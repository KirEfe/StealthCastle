using UnityEngine;

namespace StealthCastle.Isometric3D.World
{
    /// <summary>
    /// Выход из уровня для 3D изометрической версии.
    /// Триггерит событие завершения уровня при соприкосновении с игроком.
    /// </summary>
    public class LevelExit3D : MonoBehaviour
    {
        [Header("Exit Settings")]
        [SerializeField] private bool autoLoadNextLevel = true;
        [SerializeField] private string levelCompleteMessage = "Level Complete!";

        [Header("Visual Settings")]
        [SerializeField] private Renderer exitRenderer;
        [SerializeField] private Color activeColor = Color.green;
        [SerializeField] private Color inactiveColor = Color.gray;

        private bool isActive = true;
        private bool triggered = false;

        /// <summary>Событие завершения уровня</summary>
        public System.Action OnLevelCompleted;

        /// <summary>Активен ли выход</summary>
        public bool IsActive
        {
            get => isActive;
            set
            {
                isActive = value;
                UpdateVisual();
            }
        }

        private void Awake()
        {
            if (exitRenderer == null)
            {
                exitRenderer = GetComponent<Renderer>();
            }
            
            UpdateVisual();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!isActive || triggered) return;

            // Проверяем, что это игрок
            var player = other.GetComponent<Player.PlayerController3D>();
            if (player != null)
            {
                triggered = true;
                isActive = false;
                UpdateVisual();
                
                // Триггерим событие
                OnLevelCompleted?.Invoke();
                
                Debug.Log($"[LevelExit3D] {levelCompleteMessage}");
            }
        }

        private void OnTriggerExit(Collider other)
        {
            // Сбрасываем триггер при выходе игрока
            var player = other.GetComponent<Player.PlayerController3D>();
            if (player != null)
            {
                triggered = false;
                isActive = true;
                UpdateVisual();
            }
        }

        /// <summary>
        /// Обновление визуального состояния
        /// </summary>
        private void UpdateVisual()
        {
            if (exitRenderer != null)
            {
                exitRenderer.material.color = isActive ? activeColor : inactiveColor;
            }
        }

        /// <summary>
        /// Сброс состояния выхода
        /// </summary>
        public void ResetExit()
        {
            triggered = false;
            isActive = true;
            UpdateVisual();
        }

        /// <summary>
        /// Установка сообщения о завершении
        /// </summary>
        public void SetLevelCompleteMessage(string message)
        {
            levelCompleteMessage = message;
        }

        private void OnDrawGizmosSelected()
        {
            var collider = GetComponent<Collider>();
            if (collider != null)
            {
                Gizmos.color = isActive ? Color.green : Color.red;
                Gizmos.DrawWireCube(collider.bounds.center, collider.bounds.size);
            }
        }
    }
}