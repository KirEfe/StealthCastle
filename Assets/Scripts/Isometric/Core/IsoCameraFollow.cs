using UnityEngine;

namespace StealthCastle.Isometric.Core
{
    /// <summary>
    /// Камера, следующая за игроком с фиксированным изометрическим ракурсом.
    /// </summary>
    public class IsoCameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform target;          // цель слежения (игрок)
        [SerializeField] private float smoothTime = 0.15f;  // время сглаживания
        [SerializeField] private Vector3 offset = new Vector3(0f, 1f, -10f); // смещение камеры

        private Vector3 velocity;

        private void LateUpdate()
        {
            if (target == null) return;

            var targetPos = target.position + offset;
            transform.position = Vector3.SmoothDamp(transform.position, targetPos, ref velocity, smoothTime);
        }

        /// <summary>Установить цель слежения.</summary>
        public void SetTarget(Transform newTarget) => target = newTarget;
    }
}
