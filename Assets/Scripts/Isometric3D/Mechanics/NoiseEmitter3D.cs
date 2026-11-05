using System;
using UnityEngine;

namespace StealthCastle.Isometric3D.Mechanics
{
    /// <summary>
    /// Излучатель шума для 3D изометрической версии.
    /// При активации привлекает внимание врагов в радиусе действия.
    /// </summary>
    public class NoiseEmitter3D : MonoBehaviour
    {
        [Header("Noise Settings")]
        [SerializeField] private float noiseRadius = 5f; // Радиус действия шума
        [SerializeField] private float noiseIntensity = 1f; // Интенсивность шума
        [SerializeField] private LayerMask enemyLayer; // Слой врагов

        [Header("Visual Settings")]
        [SerializeField] private bool showGizmo = true;
        [SerializeField] private Color gizmoColor = Color.yellow;

        /// <summary>Событие излучения шума</summary>
        public static event Action<Vector3, float, float> OnNoiseEmitted;

        /// <summary>Радиус шума</summary>
        public float NoiseRadius => noiseRadius;

        /// <summary>Интенсивность шума</summary>
        public float NoiseIntensity => noiseIntensity;

        /// <summary>
        /// Настройка параметров шума (для объектов, созданных из кода)
        /// </summary>
        public void Configure(float radius, float intensity)
        {
            noiseRadius = radius;
            noiseIntensity = intensity;
        }

        /// <summary>
        /// Излучение шума
        /// </summary>
        public void EmitNoise()
        {
            EmitNoise(transform.position, noiseRadius, noiseIntensity);
        }

        /// <summary>
        /// Излучение шума с заданными параметрами
        /// </summary>
        public static void EmitNoise(Vector3 position, float radius, float intensity)
        {
            OnNoiseEmitted?.Invoke(position, radius, intensity);
        }

        /// <summary>
        /// Проверка, услышат ли враги этот шум
        /// </summary>
        public bool CanEnemiesHearThis(Vector3 enemyPosition)
        {
            float distance = Vector3.Distance(transform.position, enemyPosition);
            return distance <= noiseRadius * noiseIntensity;
        }

        /// <summary>
        /// Получение расстояния до врага
        /// </summary>
        public float GetDistanceToEnemy(Vector3 enemyPosition)
        {
            return Vector3.Distance(transform.position, enemyPosition);
        }

        private void OnDrawGizmosSelected()
        {
            if (!showGizmo) return;

            Gizmos.color = gizmoColor;
            Gizmos.DrawWireSphere(transform.position, noiseRadius);
        }
    }
}
