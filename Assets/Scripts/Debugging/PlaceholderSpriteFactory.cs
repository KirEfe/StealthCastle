using UnityEngine;

namespace StealthCastle.Debugging
{
    public static class PlaceholderSpriteFactory
    {
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
    }
}
