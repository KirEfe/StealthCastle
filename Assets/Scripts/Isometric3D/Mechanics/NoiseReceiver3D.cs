using UnityEngine;

namespace StealthCastle.Isometric3D.Mechanics
{
    /// <summary>
    /// Приемник шума для врагов
    /// </summary>
    public class NoiseReceiver3D : MonoBehaviour
    {
        [SerializeField] private Enemies.EnemyAI3D enemyAI;

        private void Awake()
        {
            if (enemyAI == null)
            {
                enemyAI = GetComponent<Enemies.EnemyAI3D>();
            }
        }

        private void OnEnable()
        {
            NoiseEmitter3D.OnNoiseEmitted += HandleNoise;
        }

        private void OnDisable()
        {
            NoiseEmitter3D.OnNoiseEmitted -= HandleNoise;
        }

        /// <summary>
        /// Обработка услышанного шума
        /// </summary>
        private void HandleNoise(Vector3 noisePosition, float radius, float intensity)
        {
            if (enemyAI == null) return;

            // Проверяем, находится ли враг в радиусе действия шума
            float distance = Vector3.Distance(transform.position, noisePosition);
            if (distance <= radius * intensity)
            {
                // Враг услышал шум и идёт investigate
                enemyAI.HearNoise(noisePosition);
            }
        }
    }
}