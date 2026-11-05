using UnityEngine;
using UnityEngine.InputSystem;

namespace StealthCastle.Isometric3D.Core
{
    /// <summary>
    /// Унифицированная система ввода для 3D изометрической игры.
    /// Использует Unity Input System для поддержки клавиатуры, геймпада и мыши.
    /// </summary>
    public class InputSystem3D : MonoBehaviour
    {
        [Header("Input Action Asset")]
        [SerializeField] private InputActionAsset inputActions;

        // Действия игрока
        private InputAction moveAction;
        private InputAction lookAction;
        private InputAction interactAction;
        private InputAction attackAction;
        private InputAction sprintAction;
        private InputAction crouchAction;
        private InputAction jumpAction;
        private InputAction previousAction;
        private InputAction nextAction;
        private InputAction dropDownAction;
        private InputAction pauseAction;
        private InputAction zoomAction;
        private InputAction rotateAction;

        // Текущие значения ввода
        private Vector2 moveInput;
        private Vector2 lookInput;
        private bool interactPressed;
        private bool interactHeld;
        private bool interactReleased;
        private bool attackPressed;
        private bool sprintPressed;
        private bool crouchPressed;
        private bool jumpPressed;
        private bool previousPressed;
        private bool nextPressed;
        private bool dropDownPressed;
        private bool pausePressed;
        private float zoomValue;
        private Vector2 rotateValue;

        /// <summary>Вектор движения (нормализован)</summary>
        public Vector2 MoveInput => moveInput;

        /// <summary>Вектор взгляда (мышь/правый стик)</summary>
        public Vector2 LookInput => lookInput;

        /// <summary>Было ли нажатие взаимодействия в этом каdre</summary>
        public bool InteractPressed => interactPressed;

        /// <summary>Зажато ли взаимодействие</summary>
        public bool InteractHeld => interactHeld;

        /// <summary>Было ли отпускание взаимодействия в этом каdre</summary>
        public bool InteractReleased => interactReleased;

        /// <summary>Была ли атака в этом каdre</summary>
        public bool AttackPressed => attackPressed;

        /// <summary>Зажат ли спринт</summary>
        public bool SprintPressed => sprintPressed;

        /// <summary>Было ли нажатие приседания в этом каdre</summary>
        public bool CrouchPressed => crouchPressed;

        /// <summary>Было ли нажатие прыжка в этом каdre</summary>
        public bool JumpPressed => jumpPressed;

        /// <summary>Было ли нажатие "предыдущий" в этом каdre</summary>
        public bool PreviousPressed => previousPressed;

        /// <summary>Было ли нажатие "следующий" в этом каdre</summary>
        public bool NextPressed => nextPressed;

        /// <summary>Было ли нажатие "вниз" в этом каdre</summary>
        public bool DropDownPressed => dropDownPressed;

        /// <summary>Была ли пауза в этом каdre</summary>
        public bool PausePressed => pausePressed;

        /// <summary>Значение зума (колесико мыши)</summary>
        public float ZoomValue => zoomValue;

        /// <summary>Вращение камеры</summary>
        public Vector2 RotateValue => rotateValue;

        /// <summary>Есть ли ввод от игрока</summary>
        public bool HasAnyInput => moveInput.sqrMagnitude > 0.01f || lookInput.sqrMagnitude > 0.01f;

        /// <summary>Включен ли ввод</summary>
        public bool InputEnabled { get; private set; } = true;

        /// <summary>Назначенный InputActionAsset</summary>
        public InputActionAsset InputActions => inputActions;

        private void Awake()
        {
            // При создании через AddComponent ассет назначается позже через SetInputActions
            if (inputActions != null)
            {
                BindActions();
            }
        }

        private void Start()
        {
            if (inputActions == null)
            {
                Debug.LogError("[InputSystem3D] InputActionAsset не назначен!");
            }
        }

        /// <summary>
        /// Назначение InputActionAsset в рантайме (для компонентов, созданных из кода)
        /// </summary>
        public void SetInputActions(InputActionAsset actions)
        {
            if (inputActions == actions) return;

            if (inputActions != null && isActiveAndEnabled)
            {
                inputActions.Disable();
            }

            inputActions = actions;
            if (inputActions == null) return;

            BindActions();
            if (isActiveAndEnabled)
            {
                inputActions.Enable();
            }
        }

        private void BindActions()
        {
            // Получаем действия из ассетов
            var playerMap = inputActions.FindActionMap("Player");
            if (playerMap == null)
            {
                Debug.LogError("[InputSystem3D] ActionMap 'Player' не найден!");
                return;
            }

            moveAction = playerMap.FindAction("Move");
            lookAction = playerMap.FindAction("Look");
            interactAction = playerMap.FindAction("Interact");
            attackAction = playerMap.FindAction("Attack");
            sprintAction = playerMap.FindAction("Sprint");
            crouchAction = playerMap.FindAction("Crouch");
            jumpAction = playerMap.FindAction("Jump");
            previousAction = playerMap.FindAction("Previous");
            nextAction = playerMap.FindAction("Next");
            dropDownAction = playerMap.FindAction("DropDown");

            var uiMap = inputActions.FindActionMap("UI");
            if (uiMap != null)
            {
                pauseAction = uiMap.FindAction("Cancel");
            }
        }

        private void OnEnable()
        {
            if (inputActions != null)
            {
                inputActions.Enable();
            }
        }

        private void OnDisable()
        {
            if (inputActions != null)
            {
                inputActions.Disable();
            }
        }

        private void Update()
        {
            if (!InputEnabled || inputActions == null) return;

            // Чтение ввода движения
            if (moveAction != null)
            {
                moveInput = moveAction.ReadValue<Vector2>();
            }

            // Чтение ввода взгляда
            if (lookAction != null)
            {
                lookInput = lookAction.ReadValue<Vector2>();
            }

            // Чтение взаимодействия
            UpdateInteractInput();

            // Чтение атаки
            attackPressed = attackAction != null && attackAction.WasPressedThisFrame();

            // Чтение спринта
            sprintPressed = sprintAction != null && sprintAction.IsPressed();

            // Чтение приседания
            crouchPressed = crouchAction != null && crouchAction.WasPressedThisFrame();

            // Чтение прыжка
            jumpPressed = jumpAction != null && jumpAction.WasPressedThisFrame();

            // Чтение навигации
            previousPressed = previousAction != null && previousAction.WasPressedThisFrame();
            nextPressed = nextAction != null && nextAction.WasPressedThisFrame();
            dropDownPressed = dropDownAction != null && dropDownAction.WasPressedThisFrame();

            // Чтение паузы
            pausePressed = pauseAction != null && pauseAction.WasPressedThisFrame();

            // Чтение зума
            // Проект использует только Input System — legacy Input бросает исключение
            var mouse = Mouse.current;
            zoomValue = 0f;
            rotateValue = Vector2.zero;
            if (mouse == null) return;

            float scroll = mouse.scroll.ReadValue().y;
            if (scroll != 0f)
            {
                zoomValue = Mathf.Sign(scroll);
            }

            // Чтение вращения
            if (mouse.middleButton.isPressed) // Средняя кнопка мыши
            {
                // 0.1 — чувствительность, как у legacy "Mouse X/Y"
                rotateValue = mouse.delta.ReadValue() * 0.1f;
            }
        }

        /// <summary>
        /// Обновление состояния взаимодействия
        /// </summary>
        private void UpdateInteractInput()
        {
            interactPressed = false;
            interactReleased = false;
            interactHeld = false;

            if (interactAction != null)
            {
                if (interactAction.WasPressedThisFrame())
                {
                    interactPressed = true;
                }
                else if (interactAction.WasReleasedThisFrame())
                {
                    interactReleased = true;
                }
                
                interactHeld = interactAction.IsPressed();
            }
        }

        /// <summary>
        /// Включение/отключение ввода
        /// </summary>
        public void SetInputEnabled(bool enabled)
        {
            InputEnabled = enabled;
            
            if (!enabled)
            {
                // Сбрасываем значения ввода
                moveInput = Vector2.zero;
                lookInput = Vector2.zero;
            }
        }

        /// <summary>
        /// Получение направления движения в мировых координатах
        /// </summary>
        public Vector3 GetMoveDirection()
        {
            // Для изометрии: преобразование 2D ввода в 3D направление
            // Учитываем изометрическую проекцию
            return new Vector3(moveInput.x, 0f, moveInput.y).normalized;
        }

        /// <summary>
        /// Получение вектора движения с учетом изометрии
        /// </summary>
        public Vector3 GetIsometricMoveDirection()
        {
            // Изометрическая коррекция: диагональное движение остается равномерным на экране
            const float isoYScale = 0.5774f; // ~1/√3
            return new Vector3(
                moveInput.x,
                0f,
                moveInput.y * isoYScale
            ).normalized;
        }

        /// <summary>
        /// Проверка, двигается ли игрок
        /// </summary>
        public bool IsMoving()
        {
            return moveInput.sqrMagnitude > 0.01f;
        }

        /// <summary>
        /// Получение скорости движения (0-1)
        /// </summary>
        public float GetMoveSpeed()
        {
            return Mathf.Clamp01(moveInput.magnitude);
        }

        /// <summary>
        /// Очистка ввода
        /// </summary>
        public void ClearInput()
        {
            moveInput = Vector2.zero;
            lookInput = Vector2.zero;
            interactPressed = false;
            interactHeld = false;
            interactReleased = false;
            attackPressed = false;
            sprintPressed = false;
            crouchPressed = false;
            jumpPressed = false;
            previousPressed = false;
            nextPressed = false;
            dropDownPressed = false;
            pausePressed = false;
            zoomValue = 0f;
            rotateValue = Vector2.zero;
        }
    }
}