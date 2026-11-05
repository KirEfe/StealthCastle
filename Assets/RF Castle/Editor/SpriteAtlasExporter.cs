using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>Экспорт всех Sprite ассета: имя, guid, rect, pivot.</summary>
public static class SpriteAtlasExporter
{
    [MenuItem("Tools/ExportSprites")]
    public static void Run()
    {
        var sb = new StringBuilder();
        var paths = new[]
        {
            "Assets/RF Castle/Sliced/mainlevbuild.png",
            "Assets/RF Castle/Sliced/decorative.png",
            "Assets/RF Castle/mainlevbuild.png",
            "Assets/RF Castle/decorative.png",
        };

        foreach (var path in paths)
        {
            if (!File.Exists(path))
            {
                sb.AppendLine($"NOT FOUND: {path}");
                continue;
            }

            var guid = AssetDatabase.AssetPathToGUID(path);
            sb.AppendLine($"### {path} (guid={guid})");

            var sprites = AssetDatabase.LoadAllAssetsAtPath(path);
            foreach (var obj in sprites)
            {
                if (obj is Sprite sp)
                {
                    var rect = sp.rect;
                    var pivot = sp.pivot;
                    var name = sp.name;
                    var objGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(obj));
                    sb.AppendLine($"SPRITE|{name}|rect=({rect.x},{rect.y},{rect.width},{rect.height})|pivot=({pivot.x},{pivot.y})|objGuid={objGuid}");
                }
            }
        }

        File.WriteAllText("Assets/RF Castle/sprite_map.txt", sb.ToString());
        Debug.Log($"[SpriteAtlasExporter] Экспорт готов: Assets/RF Castle/sprite_map.txt");
    }
}
