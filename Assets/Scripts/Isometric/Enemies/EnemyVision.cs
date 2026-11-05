using UnityEngine;

namespace StealthCastle.Isometric.Enemies
{
    /// <summary>
    /// Визуализация зоны обзора врага.
    /// Висит на VisionRoot (pivot в центре врага), вращается плавно по направлению
    /// от родителя-мозга через IVisionSource. Дочерний спрайт только рисуется.
    /// </summary>
    public class EnemyVision : MonoBehaviour
    {
        [Header("Плавность")]
        [SerializeField] private float rotateSpeed = 8f;  // скорость поворота зоны

        private IVisionSource source;
        private SpriteRenderer spriteRenderer;
        private float currentAngle;

        private void Awake()
        {
            // Источник взгляда — родитель (мозг врага)
            source = GetComponentInParent<IVisionSource>();
            if (source == null)
            {
                Debug.LogError("[EnemyVision] Не найден IVisionSource на родителе!", this);
                enabled = false;
                return;
            }

            // Дочерний спрайт — просто «плашка» цвета зоны
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            if (spriteRenderer == null)
            {
                Debug.LogError("[EnemyVision] Не найден дочерний SpriteRenderer!", this);
                enabled = false;
                return;
            }

            // Растягиваем спрайт один раз: длина = дальность, ширина = хорда угла
            var halfRad = source.FovDegrees * 0.5f * Mathf.Deg2Rad;
            var halfWidth = Mathf.Tan(halfRad) * source.VisionRange;
            spriteRenderer.transform.localScale = new Vector3(halfWidth * 2f, source.VisionRange, 1f);
            // Спрайт тянется вперёд от pivot (pivot = центр врага, якорь слева)
            spriteRenderer.transform.localPosition = new Vector3(halfWidth, 0f, 0f);
        }

        private void LateUpdate()
        {
            if (source == null || spriteRenderer == null) return;

            // Угол направления взгляда (в градусах, от +X против часовой)
            var targetAngle = Mathf.Atan2(source.LookDirection.y, source.LookDirection.x) * Mathf.Rad2Deg;
            // Плавное вращение pivot вокруг центра врага
            currentAngle = Mathf.LerpAngle(currentAngle, targetAngle, rotateSpeed * Time.deltaTime);
            transform.localRotation = Quaternion.Euler(0f, 0f, currentAngle);
        }
    }
}
