using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using StealthCastle.Isometric.World;

namespace StealthCastle.RfCastle.Editor
{
    /// <summary>LEGACY: построитель комнат RF Castle — отключен, т.к. PieceData изменился
    /// (Connections стал read-only, TagValue удалён). Для MVP используется
    /// IsoPiecePrefabFactory (меню Tools/RebuildPrefabsBatch).</summary>
    public static class RfBuildRooms
    {
        // [Obsolete("Используй IsoPiecePrefabFactory")]
        // private const string OutFolder = "Assets/Prefabs/World/IsoPieces_RF";
        // private const string DataFolder = "Assets/Prefabs/World/IsoPieces_RF/Data";

        // [MenuItem("Tools/RF Castle/Build Rooms (MVP)")]
        // public static void Run()
        // {
        //     UnityEngine.Debug.LogWarning("[RfBuildRooms] Отключено — используй Tools/RebuildPrefabsBatch");
        // }
    }
}
