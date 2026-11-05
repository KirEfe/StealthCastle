using UnityEngine;

namespace StealthCastle.Isometric.Mechanics
{
    /// <summary>
    /// Outline-подсветка пропа для скана маскировки.
    /// Вешается на любой проп рядом с IsoDisguisableProp (или на тот же объект).
    /// Перенесено из StealthCastle.Mechanics.DisguiseHighlight под изометрию.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class IsoDisguiseHighlight : MonoBehaviour
    {
        [Header("Настройки аутлайна")]
        [SerializeField] private float outlineScale = 1.08f;      // масштаб контура относительно спрайта
        [SerializeField] private Color candidateColor = new Color32(255, 230, 120, 255); // обычный кандидат
        [SerializeField] private Color selectedColor = Color.white;                    // выбранный

        private SpriteRenderer sourceRenderer;
        private SpriteRenderer outlineRenderer;
        private HighlightState currentState = HighlightState.None;

        private enum HighlightState { None, Candidate, Selected }

        private void Awake()
        {
            sourceRenderer = GetComponent<SpriteRenderer>();
            CreateOutlineRenderer();
            SetState(HighlightState.None);
        }

        /// <summary>Создать дочерний объект-контур (автономно, без префаба).</summary>
        private void CreateOutlineRenderer()
        {
            var outlineObject = new GameObject("Outline");
            outlineObject.transform.SetParent(transform, false);
            outlineObject.transform.SetAsFirstSibling(); // контур ПОД спрайтом по сортировке
            outlineObject.transform.localScale = Vector3.one * outlineScale;

            outlineRenderer = outlineObject.AddComponent<SpriteRenderer>();
            outlineRenderer.sortingLayerID = sourceRenderer.sortingLayerID;
            outlineRenderer.sortingOrder = sourceRenderer.sortingOrder - 1;
        }

        private void LateUpdate()
        {
            if (outlineRenderer == null || currentState == HighlightState.None)
                return;

            SyncOutlineFromSource();
        }

        /// <summary>Синхронизировать контур с внешностью пропа каждый кадр.</summary>
        private void SyncOutlineFromSource()
        {
            outlineRenderer.sprite = sourceRenderer.sprite;
            outlineRenderer.flipX = sourceRenderer.flipX;
            outlineRenderer.flipY = sourceRenderer.flipY;
            outlineRenderer.color = currentState == HighlightState.Selected ? selectedColor : candidateColor;
        }

        /// <summary>Проп в радиусе скана (жёлтый контур).</summary>
        public void SetCandidate(bool isCandidate)
        {
            if (isCandidate)
                SetState(HighlightState.Candidate);
            else if (currentState == HighlightState.Candidate)
                SetState(HighlightState.None);
        }

        /// <summary>Проп выбран как цель превращения (белый контур).</summary>
        public void SetSelected(bool isSelected)
        {
            if (isSelected)
                SetState(HighlightState.Selected);
            else if (currentState == HighlightState.Selected)
                SetState(HighlightState.None);
        }

        /// <summary>Универсальный метод: кандидат + выбран.</summary>
        public void SetHighlight(bool isCandidate, bool isSelected)
        {
            if (isSelected)
                SetState(HighlightState.Selected);
            else if (isCandidate)
                SetState(HighlightState.Candidate);
            else
                SetState(HighlightState.None);
        }

        private void SetState(HighlightState state)
        {
            currentState = state;

            if (outlineRenderer == null)
                return;

            var isVisible = state != HighlightState.None;
            outlineRenderer.enabled = isVisible;

            if (isVisible)
                SyncOutlineFromSource();
        }

        private void OnDestroy()
        {
            // Дочерний Outline уничтожится вместе с родителем — явного удаления не требуется.
        }
    }
}
