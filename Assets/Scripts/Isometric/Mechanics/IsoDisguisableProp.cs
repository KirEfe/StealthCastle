using UnityEngine;

namespace StealthCastle.Isometric.Mechanics
{
    /// <summary>
    /// Проп, в который можно замаскироваться (бочка, ящик, статуя).
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class IsoDisguisableProp : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer spriteRenderer; // спрайт пропа
        [SerializeField] private Color highlightColor = new Color32(255, 230, 120, 255); // цвет outline

        private Color baseColor;
        private bool occupied; // занят ли проп игроком

        [Header("Флаги")]
        [SerializeField] private bool isTreasure; // проп-сокровище (для Treasure-кусков)

        /// <summary>Спрайт пропа для подмены спрайта игрока.</summary>
        public Sprite Sprite => spriteRenderer != null ? spriteRenderer.sprite : null;

        /// <summary>Renderer пропа (источник внешности для маскировки).</summary>
        public SpriteRenderer Renderer => spriteRenderer;

        private IsoDisguiseHighlight highlight;

        private void Awake()
        {
            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer != null) baseColor = spriteRenderer.color;

            // Ищем IsoDisguiseHighlight: на этом же объекте или среди дочерних/родительских
            highlight = GetComponent<IsoDisguiseHighlight>()
                     ?? GetComponentInChildren<IsoDisguiseHighlight>()
                     ?? GetComponentInParent<IsoDisguiseHighlight>();
        }

        /// <summary>Подсветка пропа во время скана: outline (если есть) + смена цвета как фолбэк.</summary>
        public void SetHighlighted(bool on)
        {
            if (occupied) return;

            // Основной способ — outline
            if (highlight != null)
            {
                highlight.SetCandidate(on);
                return;
            }

            // Фолбэк для старых пропов без IsoDisguiseHighlight
            if (spriteRenderer == null) return;
            spriteRenderer.color = on ? highlightColor : baseColor;
        }

        /// <summary>Проп занят замаскированным игроком — убрать подсветку.</summary>
        public void SetOccupied(bool value)
        {
            occupied = value;
            SetHighlighted(!value);
        }
    }
}
