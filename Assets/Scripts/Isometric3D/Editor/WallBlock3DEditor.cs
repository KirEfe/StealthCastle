using StealthCastle.Isometric3D.World;
using UnityEditor;
using UnityEngine;

namespace StealthCastle.Isometric3D.EditorTools
{
    /// <summary>
    /// Инспектор стены: правка размера или стиля сразу пересобирает коллайдер и кладку
    /// </summary>
    [CustomEditor(typeof(WallBlock3D)), CanEditMultipleObjects]
    public class WallBlock3DEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("size"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("style"));
            bool changed = EditorGUI.EndChangeCheck();

            if (changed)
            {
                // Материалы следуют за стилем
                var style = (WallBlock3D.Style)serializedObject.FindProperty("style").enumValueIndex;
                var materials = WallBlocks3D.MaterialsFor(style);
                var list = serializedObject.FindProperty("materials");
                list.arraySize = materials.Length;
                for (int i = 0; i < materials.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = materials[i];
            }
            serializedObject.ApplyModifiedProperties();

            if (changed || GUILayout.Button("Пересобрать кладку"))
            {
                foreach (var t in targets)
                {
                    var wall = (WallBlock3D)t;
                    Undo.RegisterFullObjectHierarchyUndo(wall.gameObject, "Rebuild wall");
                    WallBlocks3D.Build(wall);
                }
            }

            EditorGUILayout.HelpBox("Камень — стены и колонны; дерево — столы, койки, стеллажи. " +
                                    $"Стены выше {WallBlock3D.CutawayMinHeight} м срезаются, когда закрывают игрока.", MessageType.None);
        }
    }
}
