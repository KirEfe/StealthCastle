using StealthCastle.Isometric3D.Enemies;
using StealthCastle.Isometric3D.World;
using UnityEditor;
using UnityEngine;

namespace StealthCastle.Isometric3D.EditorTools
{
    /// <summary>
    /// Инспектор стража: создать/выделить маршрут, поставить на пол. Пока страж выделен, точки его маршрута
    /// тоже можно тащить прямо в сцене.
    /// </summary>
    [CustomEditor(typeof(EnemyAI3D))]
    public class EnemyAI3DEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var guard = (EnemyAI3D)target;
            var route = serializedObject.FindProperty("route").objectReferenceValue as PatrolRoute3D;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Патруль", EditorStyles.boldLabel);
            if (route == null)
            {
                EditorGUILayout.HelpBox("Нет маршрута — страж будет стоять на месте.", MessageType.Warning);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Создать маршрут (туда-обратно)"))
                        Selection.activeGameObject = PieceAuthoring3D.CreateRoute(guard, PatrolRoute3D.RouteMode.PingPong).gameObject;
                    if (GUILayout.Button("Создать маршрут (по кругу)"))
                        Selection.activeGameObject = PieceAuthoring3D.CreateRoute(guard, PatrolRoute3D.RouteMode.Loop).gameObject;
                }
            }
            else
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Выделить маршрут")) Selection.activeGameObject = route.gameObject;
                    if (GUILayout.Button("Поставить стража в начало маршрута") && route.Count > 0)
                    {
                        Undo.RecordObject(guard.transform, "Move guard to route start");
                        var start = route.GetPoint(0);
                        guard.transform.position = new Vector3(start.x, guard.transform.position.y, start.z);
                    }
                }
            }

            // Центр капсулы должен быть на 0.9 м над полом куска: от него идут лучи зрения
            var piece = guard.GetComponentInParent<LevelPiece3D>();
            if (piece != null)
            {
                float height = piece.transform.InverseTransformPoint(guard.transform.position).y;
                if (Mathf.Abs(height - PieceAuthoring3D.BodyHeight) > 0.05f)
                {
                    EditorGUILayout.HelpBox($"Страж на высоте {height:0.00} м, нужна {PieceAuthoring3D.BodyHeight} м.", MessageType.Warning);
                    if (GUILayout.Button("Поставить на пол"))
                    {
                        Undo.RecordObject(guard.transform, "Snap guard to floor");
                        var local = piece.transform.InverseTransformPoint(guard.transform.position);
                        guard.transform.position = piece.transform.TransformPoint(new Vector3(local.x, PieceAuthoring3D.BodyHeight, local.z));
                    }
                }
            }
        }

        private void OnSceneGUI()
        {
            var route = serializedObject.FindProperty("route").objectReferenceValue as PatrolRoute3D;
            if (route != null)
            {
                PatrolRoute3DEditor.DrawHandles(route);
            }
        }
    }
}
