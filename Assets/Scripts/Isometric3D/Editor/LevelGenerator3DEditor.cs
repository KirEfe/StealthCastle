using System.Collections.Generic;
using StealthCastle.Isometric3D.World;
using UnityEditor;
using UnityEngine;

namespace StealthCastle.Isometric3D.EditorTools
{
    /// <summary>
    /// Инспектор генератора: одна кнопка подключает все куски проекта (все ассеты PieceData3D)
    /// </summary>
    [CustomEditor(typeof(LevelGenerator3D))]
    public class LevelGenerator3DEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space();
            if (GUILayout.Button(new GUIContent("Подключить все куски проекта", "Все ассеты PieceData3D — в список кусков генератора")))
            {
                Collect(serializedObject);
            }
        }

        public static int Collect(SerializedObject generator)
        {
            var pieces = new List<PieceData3D>();
            foreach (var guid in AssetDatabase.FindAssets("t:" + nameof(PieceData3D)))
            {
                var data = AssetDatabase.LoadAssetAtPath<PieceData3D>(AssetDatabase.GUIDToAssetPath(guid));
                if (data != null && data.Prefab != null) pieces.Add(data);
            }
            pieces.Sort((a, b) => string.CompareOrdinal(a.name, b.name));

            var list = generator.FindProperty("availablePieces");
            list.arraySize = pieces.Count;
            for (int i = 0; i < pieces.Count; i++)
            {
                list.GetArrayElementAtIndex(i).objectReferenceValue = pieces[i];
            }
            generator.ApplyModifiedProperties();
            Debug.Log($"[LevelGenerator3D] Подключено кусков: {pieces.Count}");
            return pieces.Count;
        }
    }
}
