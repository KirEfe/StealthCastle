using UnityEngine;

namespace StealthCastle.Isometric3D.World
{
    /// <summary>
    /// Точка появления игрока в стартовом куске (ставится на пол). Генератор переносит сюда игрока.
    /// </summary>
    public class SpawnPoint3D : MonoBehaviour
    {
        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.25f, 0.55f, 1f, 0.8f);
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.9f, 0.45f);
            Gizmos.DrawLine(transform.position, transform.position + transform.forward * 1.2f);
        }
    }
}
