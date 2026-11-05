using System.IO;
using UnityEditor;
using UnityEngine;

namespace StealthCastle.Isometric.World.Editor
{
    /// <summary>LEGACY: сборщик кусков из RF Castle — отключен, т.к. PieceData изменился
    /// (prefab/sizeInTiles/pieceTag/weight/connections стали read-only свойствами).
    /// Для MVP используется IsoPiecePrefabFactory (меню Tools/RebuildPrefabsBatch).</summary>
    public static class RoomBuilder
    {
        // [MenuItem("Tools/RF Castle/Rebuild Rooms")]
        // public static void Run()
        // {
        //     UnityEngine.Debug.LogWarning("[RoomBuilder] Отключено — используй Tools/RebuildPrefabsBatch");
        // }
    }
}
