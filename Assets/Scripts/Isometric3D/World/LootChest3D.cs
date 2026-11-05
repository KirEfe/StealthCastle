using UnityEngine;

namespace StealthCastle.Isometric3D.World
{
    /// <summary>
    /// Сундук с добычей: подошёл вплотную — добыча забега пополнилась, сундук пустой.
    /// Добыча при себе попадает в тайник хаба, только если вернуться живым.
    /// </summary>
    public class LootChest3D : MonoBehaviour
    {
        [SerializeField] private float pickupRadius = 1.3f; // От центра сундука, по полу
        [SerializeField] private Renderer bodyRenderer;
        [SerializeField] private Color openedColor = new Color(0.32f, 0.27f, 0.2f); // Сундук без крышки просто тускнеет

        [Header("Lid")]
        [SerializeField] private Transform lid; // Пивот на петлях; поворот вокруг локальной X
        [SerializeField] private float lidOpenAngle = 105f;
        [SerializeField] private float lidOpenTime = 0.35f;

        private Transform player;
        private bool opened;

        public bool Opened => opened;
        public float PickupRadius => pickupRadius;

        private void Start()
        {
            var controller = FindFirstObjectByType<Player.PlayerController3D>();
            player = controller != null ? controller.transform : null;
            if (bodyRenderer == null) bodyRenderer = GetComponentInChildren<Renderer>();
        }

        private void Update()
        {
            if (opened || player == null) return;

            var gameManager = Core.GameManager3D.Instance;
            if (gameManager == null || gameManager.CurrentState != Core.GameManager3D.GameState.Playing) return;

            var delta = player.position - transform.position;
            delta.y = 0f;
            if (delta.sqrMagnitude <= pickupRadius * pickupRadius)
            {
                Open(gameManager);
            }
        }

        private void Open(Core.GameManager3D gameManager)
        {
            opened = true;
            int amount = gameManager.CollectChest();
            if (lid != null)
            {
                StartCoroutine(OpenLid());
            }
            else if (bodyRenderer != null)
            {
                bodyRenderer.material.color = openedColor; // Экземпляр материала: тускнеет только этот сундук
            }
            Debug.Log($"[LootChest3D] +{amount} loot");
        }

        private System.Collections.IEnumerator OpenLid()
        {
            var closed = lid.localRotation;
            var open = closed * Quaternion.Euler(lidOpenAngle, 0f, 0f);
            for (float t = 0f; t < 1f; t += Time.deltaTime / lidOpenTime)
            {
                lid.localRotation = Quaternion.Slerp(closed, open, Mathf.SmoothStep(0f, 1f, t));
                yield return null;
            }
            lid.localRotation = open;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, pickupRadius);
        }
    }
}
