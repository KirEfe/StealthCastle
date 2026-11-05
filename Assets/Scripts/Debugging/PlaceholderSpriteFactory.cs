using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace StealthCastle.Debugging
{
    public static class PlaceholderSpriteFactory
    {
        // Кэш белых текстур для runtime-спрайтов (чтобы не плодить Texture2D на каждый инстанс)
        private static readonly System.Collections.Generic.Dictionary<int, Texture2D> _whiteCache = new();

        /// <summary>Белый спрайт 1x1 для UI/зон (runtime, кэшируется по PPU).</summary>
        public static Sprite CreateWhiteSprite(float pixelsPerUnit = 16f)
        {
            var key = Mathf.RoundToInt(pixelsPerUnit);
            if (!_whiteCache.TryGetValue(key, out var tex))
            {
                tex = new Texture2D(1, 1, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp
                };
                tex.SetPixel(0, 0, Color.white);
                tex.Apply();
                _whiteCache[key] = tex;
            }
            return Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), pixelsPerUnit);
        }

        /// <summary>Создать runtime-спрайт (текстура в памяти, НЕ сериализуется в префаб).</summary>
        public static Sprite CreateSquareSprite(Color color, float pixelsPerUnit = 16f)
        {
            const int size = 16;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            var pixels = new Color32[size * size];
            var fill = (Color32)color;
            for (var i = 0; i < pixels.Length; i++)
                pixels[i] = fill;

            texture.SetPixels32(pixels);
            texture.Apply();

            return Sprite.Create(
                texture,
                new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f),
                pixelsPerUnit);
        }

#if UNITY_EDITOR
        private const string AssetFolder = "Assets/Art/Sprites/Placeholders";

        /// <summary>
        /// Создать спрайт-ассет на диск (текстура + спрайт сериализуются в префаб).
        /// Кешируется по цвету — повторные вызовы возвращают тот же ассет.
        /// </summary>
        public static Sprite CreateSquareSpriteAsset(Color color, float pixelsPerUnit = 16f)
        {
            System.IO.Directory.CreateDirectory(AssetFolder);
            // Детерминированное имя по RGBA
            var c = (Color32)color;
            var name = $"px_{c.r:x2}{c.g:x2}{c.b:x2}{c.a:x2}";
            var path = $"{AssetFolder}/{name}.png";

            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (existing != null) return existing;

            const int size = 16;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            var pixels = new Color32[size * size];
            var fill = c;
            for (var i = 0; i < pixels.Length; i++)
                pixels[i] = fill;

            texture.SetPixels32(pixels);
            texture.Apply();

            // Сохраняем текстуру как PNG, импортируем, создаём спрайт
            System.IO.File.WriteAllBytes(path, texture.EncodeToPNG());
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = pixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.SaveAndReimport();

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            return sprite;
        }
#endif
    }
}
