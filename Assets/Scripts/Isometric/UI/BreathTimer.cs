using UnityEngine;

namespace StealthCastle.Isometric.UI
{
    /// <summary>
    /// HUD-полоска таймера дыхания маскировки.
    /// </summary>
    public class BreathTimer : MonoBehaviour
    {
        [SerializeField] private RectTransform fillRect;      // заполняемая часть полоски
        [SerializeField] private GameObject player;           // игрок с IsoDisguiseSystem
        [SerializeField] private CanvasGroup canvasGroup;     // скрытие, когда маскировка неактивна

        private Mechanics.IsoDisguiseSystem disguise;

        private void Start()
        {
            disguise = player != null ? player.GetComponent<Mechanics.IsoDisguiseSystem>() : null;
            if (canvasGroup != null) canvasGroup.alpha = 0f;
        }

        private void Update()
        {
            if (disguise == null) return;

            var visible = disguise.IsDisguised;
            if (canvasGroup != null) canvasGroup.alpha = visible ? 1f : 0f;
            if (!visible || fillRect == null) return;

            var fraction = disguise.RemainingTime / disguise.TotalTime;
            fillRect.anchorMax = new Vector2(fraction, 1f);
        }
    }
}
