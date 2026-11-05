using StealthCastle.Isometric3D.World;
using UnityEditor;
using UnityEngine;

namespace StealthCastle.Isometric3D.EditorTools
{
    /// <summary>
    /// Набор деталей для сборки кусков уровня: материалы стен и пола, префабы пропов, стража, выхода и т.д.
    /// Создаётся сборщиком стартового набора (LevelPieces3DBuilder); инструменты куска берут детали отсюда.
    /// </summary>
    public class LevelKit3D : ScriptableObject
    {
        [Header("Materials")]
        public Material wallMaterial;
        public Material blockerMaterial;
        [Tooltip("Пол по тегу куска: Start, Exit, Treasure, Combat, Empty")]
        public Material[] floorMaterials = new Material[5];

        [Header("Prefabs")]
        public GameObject guardPrefab;
        public GameObject cratePrefab;
        public GameObject barrelPrefab;
        public GameObject wallPrefab;
        public GameObject pillarPrefab;
        public GameObject chestPrefab;
        public GameObject exitPrefab;
        public GameObject spawnPrefab;

        [Header("Props from Blender (StealthCastle → 3D → Art)")]
        public GameObject sackPrefab;
        public GameObject kegPrefab;
        public GameObject tablePrefab;

        [Header("Layers")]
        public string groundLayer = "Ground";
        public string wallLayer = "Wall";

        public Material FloorFor(PieceData3D.Tag tag)
        {
            int index = (int)tag;
            return floorMaterials != null && index < floorMaterials.Length ? floorMaterials[index] : null;
        }

        /// <summary>Набор деталей проекта (первый найденный ассет)</summary>
        public static LevelKit3D Find()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:" + nameof(LevelKit3D)))
            {
                var kit = AssetDatabase.LoadAssetAtPath<LevelKit3D>(AssetDatabase.GUIDToAssetPath(guid));
                if (kit != null) return kit;
            }
            return null;
        }
    }
}
