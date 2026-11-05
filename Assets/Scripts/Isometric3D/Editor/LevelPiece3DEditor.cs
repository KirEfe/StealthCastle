using System.Collections.Generic;
using StealthCastle.Isometric3D.World;
using UnityEditor;
using UnityEngine;

namespace StealthCastle.Isometric3D.EditorTools
{
    /// <summary>
    /// Инспектор куска уровня: стены под проёмы, данные для генератора, проверка и палитра деталей.
    /// Работает в Prefab Mode (двойной клик по префабу куска) и на экземпляре куска в сцене.
    /// </summary>
    [CustomEditor(typeof(LevelPiece3D))]
    public class LevelPiece3DEditor : Editor
    {
        private List<string> issues;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var piece = (LevelPiece3D)target;
            var kit = LevelKit3D.Find();
            if (kit == null)
            {
                EditorGUILayout.HelpBox("Нет набора деталей (LevelKit3D). Запусти StealthCastle → 3D → Rebuild Starter Pieces.", MessageType.Error);
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Кусок", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("Перестроить пол и стены", "Пол по тегу, стены периметра и заглушки под выбранные проёмы. Внутреннее наполнение не трогается")))
                {
                    PieceAuthoring3D.RebuildShell(piece, kit, recordUndo: true);
                    issues = null;
                }
                if (GUILayout.Button(new GUIContent("Обновить PieceData", "Тег, проёмы, вес и префаб — в данные для генератора")))
                {
                    var path = PieceAuthoring3D.PrefabPathOf(piece.gameObject);
                    if (string.IsNullOrEmpty(path))
                        EditorUtility.DisplayDialog("Кусок не сохранён", "Сначала сохрани кусок как префаб (или открой префаб двойным кликом).", "OK");
                    else
                        PieceAuthoring3D.SyncData(piece, path);
                    issues = null;
                }
            }
            if (GUILayout.Button("Проверить кусок"))
            {
                issues = PieceAuthoring3D.Validate(piece);
            }
            if (issues != null)
            {
                if (issues.Count == 0) EditorGUILayout.HelpBox("Проблем не найдено.", MessageType.Info);
                foreach (var issue in issues) EditorGUILayout.HelpBox(issue, MessageType.Warning);
            }

            DrawWalkability(piece);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Добавить деталь (в центр куска, дальше двигай руками)", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                AddButton(piece, "Ящик", kit.cratePrefab, Vector3.zero);
                AddButton(piece, "Бочка", kit.barrelPrefab, Vector3.zero);
                AddButton(piece, "Стена", kit.wallPrefab, Vector3.zero);
                AddButton(piece, "Колонна", kit.pillarPrefab, Vector3.zero);
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                AddButton(piece, "Мешок", kit.sackPrefab, Vector3.zero);
                AddButton(piece, "Бочонок", kit.kegPrefab, Vector3.zero);
                AddButton(piece, "Стол", kit.tablePrefab, Vector3.zero);
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                AddButton(piece, "Сундук", kit.chestPrefab, Vector3.zero);
                AddButton(piece, "Выход", kit.exitPrefab, Vector3.zero);
                AddButton(piece, "Спавн игрока", kit.spawnPrefab, Vector3.zero);
                if (GUILayout.Button("Страж + маршрут"))
                {
                    var guard = PieceAuthoring3D.AddGuard(piece, kit, PatrolRoute3D.RouteMode.PingPong,
                        new Vector2(0f, 0f), new Vector2(0f, 3f));
                    Selection.activeGameObject = guard.gameObject;
                }
            }
        }

        // --- Проходимость ------------------------------------------------------------------

        private WalkabilityCheck3D.Report walkability;
        private Mesh walkabilityMesh;

        private void DrawWalkability(LevelPiece3D piece)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Проходимость", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("Показать сетку",
                        "Клетки 0,25 м: зелёные — капсула игрока встаёт, бледные — только впритык (уже 1,2 м), красные — занято. " +
                        "Голубой — путь между проёмами, жёлтый — узкие места на пути")))
                {
                    walkability = WalkabilityCheck3D.AnalyzePiece(piece, WalkabilityGrid3D.Settings.Default);
                    walkabilityMesh = walkability.grid.BuildOverlayMesh(walkability.Paths, walkabilityMesh);
                    SceneView.RepaintAll();
                }
                using (new EditorGUI.DisabledScope(walkability == null))
                {
                    if (GUILayout.Button("Скрыть"))
                    {
                        walkability = null;
                        SceneView.RepaintAll();
                    }
                }
            }
            if (walkability == null) return;

            foreach (var issue in walkability.issues) EditorGUILayout.HelpBox(issue, MessageType.Error);
            foreach (var warning in walkability.warnings) EditorGUILayout.HelpBox(warning, MessageType.Warning);
            foreach (var link in walkability.links)
            {
                var type = link.status switch
                {
                    WalkabilityCheck3D.Status.Ok => MessageType.Info,
                    WalkabilityCheck3D.Status.Narrow => MessageType.Warning,
                    _ => MessageType.Error,
                };
                EditorGUILayout.HelpBox($"{link.from} → {link.to}: {WalkabilityCheck3D.Describe(link.status)}", type);
            }
        }

        private void OnSceneGUI()
        {
            if (walkability == null || walkabilityMesh == null || Event.current.type != EventType.Repaint) return;
            var material = WalkabilityGrid3D.OverlayMaterial;
            material.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.LessEqual);
            material.SetPass(0);
            Graphics.DrawMeshNow(walkabilityMesh, Matrix4x4.identity);
        }

        private void OnDisable()
        {
            if (walkabilityMesh != null) DestroyImmediate(walkabilityMesh);
        }

        private static void AddButton(LevelPiece3D piece, string label, GameObject prefab, Vector3 localPosition)
        {
            using (new EditorGUI.DisabledScope(prefab == null))
            {
                if (GUILayout.Button(label))
                {
                    Selection.activeGameObject = PieceAuthoring3D.AddPart(piece, prefab, localPosition, "Add " + label);
                }
            }
        }
    }
}
