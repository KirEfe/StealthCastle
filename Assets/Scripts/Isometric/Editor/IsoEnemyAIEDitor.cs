using UnityEditor;
using UnityEngine;
using StealthCastle.Isometric.Enemies;

namespace StealthCastle.Isometric.Editor
{
    /// <summary>
    /// Кастомный инспектор IsoEnemyAI: отрисовка маршрута патруля прямо в инспекторе,
    /// drag'n'drop точек в Scene View, авто-расстановка и валидация (точки не в стенах).
    /// </summary>
    [CustomEditor(typeof(IsoEnemyAI))]
    public class IsoEnemyAIEDitor : UnityEditor.Editor
    {
        private SerializedProperty waypointsProp;
        private SerializedProperty patrolSpeedProp;
        private SerializedProperty waypointReachDistProp;
        private SerializedProperty waitAtWaypointProp;
        private SerializedProperty visionRangeProp;
        private SerializedProperty fovDegreesProp;
        private SerializedProperty chaseSpeedProp;
        private SerializedProperty crawlDetectDistanceProp;
        private SerializedProperty loseSightTimeProp;
        private SerializedProperty wallMaskProp;
        private SerializedProperty searchDurationProp;
        private SerializedProperty suspicionRangeProp;
        private SerializedProperty propCheckCooldownProp;
        private SerializedProperty lookBackDurationProp;
        private SerializedProperty guardSpriteProp;

        private bool showWaypoints = true;
        private int selectedWaypointIndex = -1;
        private const float HandleSize = 0.25f;

        private void OnEnable()
        {
            waypointsProp = serializedObject.FindProperty("waypoints");
            patrolSpeedProp = serializedObject.FindProperty("patrolSpeed");
            waypointReachDistProp = serializedObject.FindProperty("waypointReachDist");
            waitAtWaypointProp = serializedObject.FindProperty("waitAtWaypoint");
            visionRangeProp = serializedObject.FindProperty("visionRange");
            fovDegreesProp = serializedObject.FindProperty("fovDegrees");
            chaseSpeedProp = serializedObject.FindProperty("chaseSpeed");
            crawlDetectDistanceProp = serializedObject.FindProperty("crawlDetectDistance");
            loseSightTimeProp = serializedObject.FindProperty("loseSightTime");
            wallMaskProp = serializedObject.FindProperty("wallMask");
            searchDurationProp = serializedObject.FindProperty("searchDuration");
            suspicionRangeProp = serializedObject.FindProperty("suspicionRange");
            propCheckCooldownProp = serializedObject.FindProperty("propCheckCooldown");
            lookBackDurationProp = serializedObject.FindProperty("lookBackDuration");
            guardSpriteProp = serializedObject.FindProperty("guardSprite");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var ai = (IsoEnemyAI)target;

            // Заголовок с кнопками действий
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Маршрут патруля", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Расставить по комнате", GUILayout.Height(24)))
                AutoArrangeWaypoints(ai);
            if (GUILayout.Button("+ Точка", GUILayout.Height(24)))
                AddWaypoint();
            if (GUILayout.Button("− Последнюю", GUILayout.Height(24)))
                RemoveLastWaypoint();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.HelpBox(
                "Оранжевые точки — маршрут. Первую точку можно перетаскивать в Scene View. " +
                "Красные точки — проблема (в стене / вне комнаты).",
                MessageType.Info);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(6f);

            // Список точек с валидацией
            showWaypoints = EditorGUILayout.Foldout(showWaypoints, $"Точки ({waypointsProp.arraySize})", true);
            if (showWaypoints)
            {
                EditorGUI.indentLevel++;
                for (var i = 0; i < waypointsProp.arraySize; i++)
                {
                    var elem = waypointsProp.GetArrayElementAtIndex(i);
                    var wp = elem.vector3Value;
                    var isValid = !IsPointInWall(ai, wp, out var wallName);

                    EditorGUILayout.BeginHorizontal();
                    EditorGUI.BeginChangeCheck();
                    var newWp = EditorGUILayout.Vector3Field(
                        (i == 0 ? "★ " : $"{i}. ") + wp.ToString("F2"), wp);
                    if (EditorGUI.EndChangeCheck())
                    {
                        elem.vector3Value = newWp;
                        selectedWaypointIndex = i;
                        SceneView.RepaintAll();
                    }

                    // Кнопка удаления конкретной точки
                    if (GUILayout.Button("×", GUILayout.Width(24)))
                    {
                        RemoveWaypointAt(i);
                        break;
                    }
                    EditorGUILayout.EndHorizontal();

                    if (!isValid)
                    {
                        EditorGUILayout.HelpBox($"Точка {i} пересекается со стеной: {wallName}", MessageType.Warning);
                    }
                }
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(6f);

            // Параметры патруля
            EditorGUILayout.LabelField("Патруль", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(patrolSpeedProp);
            EditorGUILayout.PropertyField(waypointReachDistProp);
            EditorGUILayout.PropertyField(waitAtWaypointProp);

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Обнаружение", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(visionRangeProp);
            EditorGUILayout.PropertyField(fovDegreesProp);
            EditorGUILayout.PropertyField(chaseSpeedProp);
            EditorGUILayout.PropertyField(crawlDetectDistanceProp);
            EditorGUILayout.PropertyField(loseSightTimeProp);
            EditorGUILayout.PropertyField(wallMaskProp);

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Поиск и подозрение", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(searchDurationProp);
            EditorGUILayout.PropertyField(suspicionRangeProp);
            EditorGUILayout.PropertyField(propCheckCooldownProp);
            EditorGUILayout.PropertyField(lookBackDurationProp);

            EditorGUILayout.Space(6f);
            EditorGUILayout.PropertyField(guardSpriteProp);

            serializedObject.ApplyModifiedProperties();

            // Сцена всегда перерисовывается, пока выбран враг (для хендлов)
            if (SceneView.lastActiveSceneView != null)
                SceneView.lastActiveSceneView.Repaint();
        }

        // Отрисовка хендлов в Scene View: перетаскивание первой точки + маршрут
        private void OnSceneGUI()
        {
            var ai = (IsoEnemyAI)target;
            if (waypointsProp == null || waypointsProp.arraySize == 0) return;

            var root = ai.transform.parent != null ? ai.transform.parent : ai.transform;
            var worldMatrix = Matrix4x4.TRS(root.position, root.rotation, root.lossyScale);

            // Рисуем маршрут
            Handles.color = new Color(1f, 0.85f, 0f, 0.9f);
            for (var i = 0; i < waypointsProp.arraySize; i++)
            {
                var next = (i + 1) % waypointsProp.arraySize;
                var a = worldMatrix.MultiplyPoint3x4(waypointsProp.GetArrayElementAtIndex(i).vector3Value);
                var b = worldMatrix.MultiplyPoint3x4(waypointsProp.GetArrayElementAtIndex(next).vector3Value);
                Handles.DrawLine(a, b);
            }

            // Хендлы точек: первую можно таскать мышью
            for (var i = 0; i < waypointsProp.arraySize; i++)
            {
                var elem = waypointsProp.GetArrayElementAtIndex(i);
                var localWp = elem.vector3Value;
                var worldWp = worldMatrix.MultiplyPoint3x4(localWp);

                var isValid = !IsPointInWall(ai, localWp, out _);
                Handles.color = isValid ? (i == 0 ? Color.green : new Color(1f, 0.85f, 0f, 0.9f)) : Color.red;

                if (i == 0)
                {
                    // Первую точку можно перетаскивать
                    EditorGUI.BeginChangeCheck();
                    var fmh_187_25_639268876391554363 = Quaternion.identity; var newPos = Handles.FreeMoveHandle(
                        worldWp,
                        HandleSize,
                        Vector3.one * 0.05f,
                        Handles.SphereHandleCap);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(ai, "Перемещение точки патруля");
                        elem.vector3Value = worldMatrix.inverse.MultiplyPoint3x4(newPos);
                        serializedObject.ApplyModifiedProperties();
                    }
                }
                else
                {
                    // Остальные — только визуальные маркеры
                    Handles.SphereHandleCap(0, worldWp, Quaternion.identity, HandleSize, EventType.Repaint);
                }
            }
        }

        // Авто-расстановка по границам комнаты (аналог ContextMenu, но через Undo)
        private void AutoArrangeWaypoints(IsoEnemyAI ai)
        {
            Undo.RecordObject(ai, "Авто-расстановка точек патруля");
            ai.AutoArrangeWaypoints();
            waypointsProp.arraySize = ai.PatrolWaypoints.Count;
            for (var i = 0; i < ai.PatrolWaypoints.Count; i++)
                waypointsProp.GetArrayElementAtIndex(i).vector3Value = ai.PatrolWaypoints[i];
            serializedObject.ApplyModifiedProperties();
        }

        private void AddWaypoint()
        {
            Undo.RecordObject(target, "Добавить точку патруля");
            waypointsProp.InsertArrayElementAtIndex(waypointsProp.arraySize);
            var last = waypointsProp.GetArrayElementAtIndex(waypointsProp.arraySize - 1);
            // Новая точка рядом с последней (или в нуле, если первая)
            if (waypointsProp.arraySize > 1)
            {
                var prev = waypointsProp.GetArrayElementAtIndex(waypointsProp.arraySize - 2).vector3Value;
                last.vector3Value = prev + Vector3.right;
            }
            else
            {
                last.vector3Value = Vector3.zero;
            }
            serializedObject.ApplyModifiedProperties();
        }

        private void RemoveLastWaypoint()
        {
            if (waypointsProp.arraySize == 0) return;
            Undo.RecordObject(target, "Удалить точку патруля");
            waypointsProp.DeleteArrayElementAtIndex(waypointsProp.arraySize - 1);
            serializedObject.ApplyModifiedProperties();
        }

        private void RemoveWaypointAt(int index)
        {
            Undo.RecordObject(target, "Удалить точку патруля");
            waypointsProp.DeleteArrayElementAtIndex(index);
            serializedObject.ApplyModifiedProperties();
        }

        // Проверка: попадает ли точка в стену (Raycast вниз на слой wallMask)
        private bool IsPointInWall(IsoEnemyAI ai, Vector3 localPoint, out string wallName)
        {
            wallName = "";
            var root = ai.transform.parent != null ? ai.transform.parent : ai.transform;
            var worldPoint = root.TransformPoint(localPoint);

            // Raycast строго вниз — стена под точкой
            if (Physics2D.Raycast(worldPoint, Vector2.down, 0.1f, ai.WallMask))
            {
                wallName = "пол/стена под точкой";
                return true;
            }

            // Дополнительно: горизонтальный raycast в 4 стороны на 0.3 м — стена рядом
            var dirs = new[] { Vector2.right, Vector2.left, Vector2.up, Vector2.down };
            foreach (var d in dirs)
            {
                if (Physics2D.Raycast(worldPoint, d, 0.3f, ai.WallMask))
                {
                    wallName = "стена рядом";
                    return true;
                }
            }

            return false;
        }
    }
}
