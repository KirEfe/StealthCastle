using UnityEngine;

namespace StealthCastle.Mechanics
{
    [RequireComponent(typeof(SpriteRenderer))]
    public class DisguisableProp : MonoBehaviour
    {
        [SerializeField] string displayName;

        SpriteRenderer spriteRenderer;

        public string DisplayName => string.IsNullOrEmpty(displayName) ? gameObject.name : displayName;
        public SpriteRenderer SpriteRenderer => spriteRenderer;

        void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }
    }
}
