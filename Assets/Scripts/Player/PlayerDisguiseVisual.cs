using UnityEngine;

namespace StealthCastle.Player
{
    [RequireComponent(typeof(SpriteRenderer))]
    public class PlayerDisguiseVisual : MonoBehaviour
    {
        SpriteRenderer spriteRenderer;
        Sprite originalSprite;
        Color originalColor;
        bool originalFlipX;

        void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            CacheOriginalAppearance();
        }

        void CacheOriginalAppearance()
        {
            originalSprite = spriteRenderer.sprite;
            originalColor = spriteRenderer.color;
            originalFlipX = spriteRenderer.flipX;
        }

        public void ApplyDisguise(SpriteRenderer source)
        {
            if (source == null)
                return;

            spriteRenderer.sprite = source.sprite;
            spriteRenderer.color = source.color;
            spriteRenderer.flipX = source.flipX;
        }

        public void ClearDisguise()
        {
            spriteRenderer.sprite = originalSprite;
            spriteRenderer.color = originalColor;
            spriteRenderer.flipX = originalFlipX;
        }
    }
}
