using UnityEngine;
using UnityEngine.InputSystem;

namespace StealthCastle.Isometric.Player
{
    /// <summary>
    /// Изометрический контроллер игрока (этап 0 прототипа).
    /// Свободное движение а-ля Hades: WASD, 8 направлений, без гравитации.
    /// Ввод — через Input System (Player/Move).
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class IsoPlayerController : MonoBehaviour
    {
        [Header("Движение")]
        [SerializeField] private float moveSpeed = 5f;          // скорость игрока, м/с
        [SerializeField] private float crawlSpeed = 1.5f;       // скорость ползания в маскировке
        [SerializeField] private float isoYScale = 0.5774f;     // коррекция вертикальной оси изометрии (~1/√3)

        // Множители скорости по осям: вертикальный ввод замедляется под ракурс изометрии
        private static readonly Vector2 IsoAxisScale = new Vector2(1f, 0.7071f);

        private Rigidbody2D rb;
        private Vector2 moveInput;
        private bool inputEnabled;
        private StealthCastle.Isometric.Mechanics.IsoDisguiseSystem disguiseSystem;
        private InputSystem_Actions inputActions; // wrapper для корректного Dispose
        private InputAction moveAction;

        /// <summary>Активна ли маскировка (влияет на скорость).</summary>
        public bool IsCrawling => disguiseSystem != null && disguiseSystem.IsDisguised;

        /// <summary>Движется ли игрок сейчас (для обнаружения ползущего врагом).</summary>
        public bool IsMoving() => rb.linearVelocity.sqrMagnitude > 0.01f;

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            // Топ-даун вид: гравитация не нужна, вращение запрещено
            rb.gravityScale = 0f;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            disguiseSystem = GetComponent<StealthCastle.Isometric.Mechanics.IsoDisguiseSystem>();

            // Привязка к существующему экшену Player/Move
            inputActions = new InputSystem_Actions();
            moveAction = inputActions.Player.Move;
            moveAction.Enable();
            inputEnabled = true;
        }

        private void OnDestroy()
        {
            // Dispose освобождает ссылку на ассет (иначе Finalize бросает Assert об утечке)
            inputActions?.Dispose();
        }

        private void Update()
        {
            if (!inputEnabled || moveAction == null)
            {
                moveInput = Vector2.zero;
                return;
            }

            // Чтение вектора движения из Input System (уже нормализован для геймпада/клавиатуры)
            moveInput = moveAction.ReadValue<Vector2>();
        }

        private void FixedUpdate()
        {
            // Коррекция осей под изометрию: диагональ остаётся равномерной на экране
            var dir = new Vector2(
                moveInput.x * IsoAxisScale.x,
                moveInput.y * IsoAxisScale.y);

            var speed = IsCrawling ? crawlSpeed : moveSpeed;
            rb.linearVelocity = dir * speed;
        }

        /// <summary>Включение/выключение управления (на время сборки уровня).</summary>
        public void SetInputEnabled(bool enabled)
        {
            inputEnabled = enabled;
            if (!enabled)
            {
                moveInput = Vector2.zero;
                rb.linearVelocity = Vector2.zero;
            }
        }
    }
}
