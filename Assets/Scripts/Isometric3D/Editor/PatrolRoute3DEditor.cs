using StealthCastle.Isometric3D.World;
using UnityEditor;
using UnityEngine;

namespace StealthCastle.Isometric3D.EditorTools
{
    /// <summary>
    /// Маршрут патруля в сцене: у выделенного маршрута каждую точку можно тащить мышью прямо по полу,
    /// рядом подписан её номер. Добавить точку — кнопкой (встаёт продолжением последнего отрезка) или Ctrl+D на точке.
    /// </summary>
    [CustomEditor(typeof(PatrolRoute3D))]
    public class PatrolRoute3DEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var route = (PatrolRoute3D)target;

            EditorGUILayout.HelpBox("Точки — дочерние объекты, порядок обхода — порядок в иерархии. " +
                                    "Тащи точки за кружки в сцене. Одна точка — часовой на посту.", MessageType.None);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Добавить точку")) AddPointAfterLast(route);
                using (new EditorGUI.DisabledScope(route.Count < 2))
                {
                    if (GUILayout.Button("Развернуть порядок")) Reverse(route);
                }
            }
        }

        private void OnSceneGUI()
        {
            DrawHandles((PatrolRoute3D)target);
        }

        /// <summary>Ручки точек по полу; используется и из инспектора стража</summary>
        public static void DrawHandles(PatrolRoute3D route)
        {
            for (int i = 0; i < route.Count; i++)
            {
                var point = route.transform.GetChild(i);
                Vector3 position = point.position;
                float size = HandleUtility.GetHandleSize(position) * 0.12f;

                Handles.color = i == 0 ? new Color(1f, 0.85f, 0.2f) : new Color(1f, 0.55f, 0.1f);
                EditorGUI.BeginChangeCheck();
                // Перетаскивание только в плоскости пола: высота точки не меняется
                Vector3 moved = Handles.Slider2D(position, Vector3.up, Vector3.right, Vector3.forward, size, Handles.CircleHandleCap, Vector2.zero);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(point, "Move patrol point");
                    point.position = new Vector3(moved.x, position.y, moved.z);
                }

                Handles.Label(position + Vector3.up * 0.4f, i.ToString(), EditorStyles.boldLabel);
            }
        }

        private static void AddPointAfterLast(PatrolRoute3D route)
        {
            Vector3 local = Vector3.zero;
            int count = route.Count;
            if (count >= 2)
            {
                // Продолжение последнего отрезка на 2 м
                var last = route.transform.GetChild(count - 1).localPosition;
                var before = route.transform.GetChild(count - 2).localPosition;
                var step = last - before;
                local = last + (step.sqrMagnitude > 0.01f ? step.normalized * 2f : Vector3.right * 2f);
            }
            else if (count == 1)
            {
                local = route.transform.GetChild(0).localPosition + Vector3.right * 2f;
            }

            Selection.activeTransform = PieceAuthoring3D.AddPoint(route, local);
        }

        private static void Reverse(PatrolRoute3D route)
        {
            int count = route.Count;
            var children = new Transform[count];
            for (int i = 0; i < count; i++) children[i] = route.transform.GetChild(i);
            for (int i = 0; i < count; i++)
            {
                Undo.SetTransformParent(children[i], route.transform, "Reverse patrol route");
                children[i].SetSiblingIndex(count - 1 - i);
            }
        }
    }
}
