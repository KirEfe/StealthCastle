using StealthCastle.Mechanics;
using UnityEngine;

namespace StealthCastle.Debugging
{
    public class DisguiseTestBootstrap : MonoBehaviour
    {
        [SerializeField] SpriteRenderer playerRenderer;
        [SerializeField] SpriteRenderer barrelRenderer;
        [SerializeField] SpriteRenderer crateRenderer;

        void Awake()
        {
            AssignPlaceholder(playerRenderer, new Color(0.2f, 0.65f, 1f), 1.2f);
            AssignPlaceholder(barrelRenderer, new Color(0.55f, 0.35f, 0.15f), 1.5f);
            AssignPlaceholder(crateRenderer, new Color(0.75f, 0.55f, 0.25f), 1.3f);
        }

        static void AssignPlaceholder(SpriteRenderer renderer, Color color, float scale)
        {
            if (renderer == null)
                return;

            renderer.sprite = PlaceholderSpriteFactory.CreateSquareSprite(color);
            renderer.transform.localScale = Vector3.one * scale;
        }
    }
}
