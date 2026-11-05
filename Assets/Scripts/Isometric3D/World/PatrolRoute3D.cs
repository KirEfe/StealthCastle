using UnityEngine;

namespace StealthCastle.Isometric3D.World
{
    /// <summary>
    /// Маршрут патруля: точки — дочерние объекты в порядке иерархии. Точки двигаются руками в сцене
    /// (у выделенного маршрута у каждой точки есть ручка), добавляются кнопкой в инспекторе или Ctrl+D.
    /// Маршрут — отдельный объект в куске уровня (не дочерний стражу), поэтому точки не уезжают вместе со стражем.
    /// Одна точка — часовой: стоит на месте и оглядывается.
    /// </summary>
    public class PatrolRoute3D : MonoBehaviour
    {
        public enum RouteMode
        {
            Loop,     // 0 → 1 → … → последняя → 0
            PingPong, // 0 → … → последняя → … → 0
        }

        [SerializeField] private RouteMode mode = RouteMode.Loop;
        [SerializeField] private Color gizmoColor = new Color(1f, 0.55f, 0.1f);

        public RouteMode Mode => mode;
        public int Count => transform.childCount;

        /// <summary>Точка маршрута в мире</summary>
        public Vector3 GetPoint(int index) => transform.GetChild(index).position;

        /// <summary>Следующая точка; direction хранит направление обхода для режима «туда-обратно»</summary>
        public int Next(int index, ref int direction)
        {
            int count = Count;
            if (count < 2) return 0;

            if (mode == RouteMode.Loop) return (index + 1) % count;

            if (direction == 0) direction = 1;
            int next = index + direction;
            if (next < 0 || next >= count)
            {
                direction = -direction;
                next = index + direction;
            }
            return next;
        }

        private void OnDrawGizmos() => DrawRoute(0.45f);

        private void OnDrawGizmosSelected() => DrawRoute(1f);

        private void DrawRoute(float alpha)
        {
            int count = Count;
            if (count == 0) return;

            Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, alpha);
            for (int i = 0; i < count; i++)
            {
                Vector3 point = GetPoint(i);
                Gizmos.DrawSphere(point, i == 0 ? 0.3f : 0.2f);
                if (i + 1 < count)
                {
                    Gizmos.DrawLine(point, GetPoint(i + 1));
                }
            }
            if (mode == RouteMode.Loop && count > 2)
            {
                Gizmos.DrawLine(GetPoint(count - 1), GetPoint(0));
            }
        }
    }
}
