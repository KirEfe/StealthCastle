using UnityEngine;

namespace StealthCastle.Isometric3D.World
{
    /// <summary>
    /// Срез стен: верх стены опускается до цоколя, если стена стоит между камерой и игроком
    /// (ближе к камере, чем игрок, и в полосе lateralRange от него по горизонтали экрана).
    /// На общем плане уровня срезаны все стены — уровень читается как план.
    /// Создаётся сам при первой стене со срезом.
    /// </summary>
    public class WallCutaway3D : MonoBehaviour
    {
        [SerializeField] private float lateralRange = 6f;   // Полоса по горизонтали экрана, м
        [SerializeField] private float depthRange = 9f;     // Насколько ближе к камере ещё срезаем, м
        [SerializeField] private float behindMargin = -0.6f; // Стена, начинающаяся дальше этого (к камере — минус), остаётся

        private static WallCutaway3D instance;

        private Camera cachedCamera;
        private Core.CameraSystem3D cameraSystem;
        private Transform player;
        private float searchTimer;

        /// <summary>Контроллер на сцену (создаётся при первой стене со срезом)</summary>
        public static void Ensure()
        {
            if (instance != null || !Application.isPlaying) return;
            instance = new GameObject("WallCutaway3D").AddComponent<WallCutaway3D>();
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        private void LateUpdate()
        {
            FindTargets();
            if (cachedCamera == null) return;

            bool overview = cameraSystem != null && cameraSystem.IsOverview;
            var forward = cachedCamera.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
            forward.Normalize();
            var right = new Vector3(forward.z, 0f, -forward.x);

            foreach (var wall in WallBlock3D.Active)
            {
                bool lowered = overview || (player != null && Occludes(wall.Footprint, player.position, forward, right));
                wall.SetCutaway(lowered);
            }
        }

        /// <summary>Пятно стены (прямоугольник XZ) — между камерой и игроком?</summary>
        private bool Occludes(Bounds footprint, Vector3 target, Vector3 forward, Vector3 right)
        {
            float alongMin = float.MaxValue, alongMax = float.MinValue, sideMin = float.MaxValue, sideMax = float.MinValue;
            for (int i = 0; i < 4; i++)
            {
                var corner = new Vector3(i < 2 ? footprint.min.x : footprint.max.x, 0f, i % 2 == 0 ? footprint.min.z : footprint.max.z);
                var delta = corner - target;
                delta.y = 0f;
                float along = Vector3.Dot(delta, forward);
                float side = Vector3.Dot(delta, right);
                alongMin = Mathf.Min(alongMin, along);
                alongMax = Mathf.Max(alongMax, along);
                sideMin = Mathf.Min(sideMin, side);
                sideMax = Mathf.Max(sideMax, side);
            }
            // along < 0 — ближе к камере, чем игрок
            return alongMin < behindMargin && alongMax > -depthRange && sideMax > -lateralRange && sideMin < lateralRange;
        }

        private void FindTargets()
        {
            if (cachedCamera != null && player != null) return;
            searchTimer -= Time.unscaledDeltaTime;
            if (searchTimer > 0f) return;
            searchTimer = 0.5f;

            if (cachedCamera == null)
            {
                cachedCamera = Camera.main;
                cameraSystem = cachedCamera != null ? cachedCamera.GetComponent<Core.CameraSystem3D>() : null;
            }
            if (player == null)
            {
                var controller = FindFirstObjectByType<Player.PlayerController3D>();
                player = controller != null ? controller.transform : null;
            }
        }
    }
}
