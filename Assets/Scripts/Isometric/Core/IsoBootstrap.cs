using UnityEngine;

namespace StealthCastle.Isometric.Core
{
    /// <summary>
    /// Бутстрап изометрического прототипа: настраивает сортировку спрайтов по Y
    /// (Transparency Sort Mode = Custom, ось Y) и создаёт тестовую сцену с плейсхолдерами.
    /// Повесить на пустой GameObject в новой сцене.
    /// </summary>
    public class IsoBootstrap : MonoBehaviour
    {
        [SerializeField] private float playerSpeed = 5f;

        private void Awake()
        {
            ConfigureSorting();
            BuildTestScene();
        }

        // Сортировка по Y — обязательна для чистой 2D-изометрии
        private static void ConfigureSorting()
        {
            var camera = Camera.main;
            if (camera != null)
            {
                camera.transparencySortMode = TransparencySortMode.CustomAxis;
                camera.transparencySortAxis = new Vector3(0f, 1f, 0f);
            }
        }

        // Тестовая арена: пол + стены-плейсхолдеры + игрок
        private void BuildTestScene()
        {
            var floor = CreateSprite("Floor", new Color32(70, 70, 90, 255), new Vector2(20f, 12f));
            floor.transform.position = Vector3.zero;
            floor.transform.localScale = new Vector3(1f, 0.5774f, 1f); // сплющиваем под изометрию

            // Игрок
            var playerGo = new GameObject("Player");
            playerGo.transform.position = new Vector3(0f, -2f, 0f);
            var playerRb = playerGo.AddComponent<Rigidbody2D>();
            playerRb.gravityScale = 0f;
            var col = playerGo.AddComponent<CircleCollider2D>();
            col.radius = 0.35f;
            var sr = playerGo.AddComponent<SpriteRenderer>();
            sr.sprite = Debugging.PlaceholderSpriteFactory.CreateSquareSprite(new Color32(220, 200, 120, 255));
            sr.sortingOrder = 10;
            playerGo.AddComponent<Isometric.Player.IsoPlayerController>();

            var cam = Camera.main;
            if (cam != null)
            {
                cam.orthographic = true;
                cam.orthographicSize = 7f;
                var follow = cam.gameObject.GetComponent<IsoCameraFollow>()
                             ?? cam.gameObject.AddComponent<IsoCameraFollow>();
                follow.SetTarget(playerGo.transform);
            }
        }

        // Создаёт GameObject со SpriteRenderer и квадратным спрайтом заданного цвета
        private static GameObject CreateSprite(string name, Color color, Vector2 size)
        {
            var go = new GameObject(name);
            go.transform.localScale = size;
            go.AddComponent<SpriteRenderer>().sprite =
                Debugging.PlaceholderSpriteFactory.CreateSquareSprite(color);
            return go;
        }
    }
}
