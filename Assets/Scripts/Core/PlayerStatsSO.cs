using UnityEngine;

namespace StealthCastle.Core
{
    [CreateAssetMenu(fileName = "PlayerStats", menuName = "StealthCastle/PlayerStats")]
    public class PlayerStatsSO : ScriptableObject
    {
        [Tooltip("Максимальное количество здоровья игрока")]
        public float maxHealth = 100f;
    }
}