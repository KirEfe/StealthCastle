using UnityEngine;

namespace StealthCastle.Mechanics
{
    [RequireComponent(typeof(SpriteRenderer))]
    public class DisguiseHighlight : MonoBehaviour
    {
        [SerializeField] float outlineScale = 1.08f;
        [SerializeField] Color candidateColor = new(1f, 0.92f, 0.016f, 1f);
        [SerializeField] Color selectedColor = Color.white;

        SpriteRenderer sourceRenderer;
        SpriteRenderer outlineRenderer;
        HighlightState currentState = HighlightState.None;

        enum HighlightState
        {
            None,
            Candidate,
            Selected
        }

        void Awake()
        {
            sourceRenderer = GetComponent<SpriteRenderer>();
            CreateOutlineRenderer();
            SetState(HighlightState.None);
        }

        void LateUpdate()
        {
            if (outlineRenderer == null || currentState == HighlightState.None)
                return;

            SyncOutlineFromSource();
        }

        void CreateOutlineRenderer()
        {
            var outlineObject = new GameObject("Outline");
            outlineObject.transform.SetParent(transform, false);
            outlineObject.transform.localScale = Vector3.one * outlineScale;

            outlineRenderer = outlineObject.AddComponent<SpriteRenderer>();
            outlineRenderer.sortingLayerID = sourceRenderer.sortingLayerID;
            outlineRenderer.sortingOrder = sourceRenderer.sortingOrder - 1;
        }

        void SyncOutlineFromSource()
        {
            outlineRenderer.sprite = sourceRenderer.sprite;
            outlineRenderer.flipX = sourceRenderer.flipX;
            outlineRenderer.flipY = sourceRenderer.flipY;
            outlineRenderer.color = currentState == HighlightState.Selected ? selectedColor : candidateColor;
        }

        public void SetCandidate(bool isCandidate)
        {
            if (isCandidate)
                SetState(HighlightState.Candidate);
            else if (currentState == HighlightState.Candidate)
                SetState(HighlightState.None);
        }

        public void SetSelected(bool isSelected)
        {
            if (isSelected)
                SetState(HighlightState.Selected);
            else if (currentState == HighlightState.Selected)
                SetState(HighlightState.None);
        }

        public void SetHighlight(bool isCandidate, bool isSelected)
        {
            if (isSelected)
                SetState(HighlightState.Selected);
            else if (isCandidate)
                SetState(HighlightState.Candidate);
            else
                SetState(HighlightState.None);
        }

        void SetState(HighlightState state)
        {
            currentState = state;

            if (outlineRenderer == null)
                return;

            var isVisible = state != HighlightState.None;
            outlineRenderer.enabled = isVisible;

            if (isVisible)
                SyncOutlineFromSource();
        }
    }
}
