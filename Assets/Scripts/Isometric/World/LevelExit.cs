using UnityEngine;

namespace StealthCastle.Isometric.World
{
    /// <summary>
    /// Триггер выхода из уровня. Игрок, зашедший в зону, завершает забег победой.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class LevelExit : MonoBehaviour
    {
        private void Awake()
        {
            // Коллайдер должен быть триггером
            var col = GetComponent<Collider2D>();
            if (col != null) col.isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            // Только игрок с IsoPlayerController завершает уровень
            if (other.GetComponent<Player.IsoPlayerController>() != null)
                Core.RunManager.Instance?.OnLevelCompleted();
        }
    }
}
