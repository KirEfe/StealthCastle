using UnityEngine;
using UnityEngine.InputSystem;

namespace StealthCastle.Isometric3D.Setup
{
    /// <summary>
    /// Настройка Input Actions для 3D изометрической версии.
    /// Создает и настраивает InputActionAsset программно, если он не найден.
    /// </summary>
    public class InputActionsSetup3D : MonoBehaviour
    {
        [Header("Input Actions Setup")]
        [SerializeField] private bool autoCreateInputActions = true;
        [SerializeField] private string actionsAssetName = "InputSystem_Actions";

        private void Awake()
        {
            if (autoCreateInputActions)
            {
                EnsureInputActionsExist();
            }
        }

        /// <summary>
        /// Проверка и создание Input Actions при необходимости
        /// </summary>
        private void EnsureInputActionsExist()
        {
            var inputSystem = FindObjectOfType<Core.InputSystem3D>();
            if (inputSystem != null && inputSystem.InputActions != null) return;

            // Проверяем, есть ли уже InputActionAsset
            var inputActions = Resources.Load<InputActionAsset>(actionsAssetName);

            if (inputActions != null)
            {
                Debug.Log("[InputActionsSetup3D] Input Actions asset found: " + actionsAssetName);
                if (inputSystem != null) inputSystem.SetInputActions(inputActions);
                return;
            }
            
            Debug.LogWarning("[InputActionsSetup3D] Input Actions asset not found. Creating programmatic setup...");
            
            // Создаем InputActionAsset программно
            CreateInputActionsAsset();
        }

        /// <summary>
        /// Создание InputActionAsset программно
        /// </summary>
        private void CreateInputActionsAsset()
        {
            // В Unity Input System можно создавать InputActionAsset программно
            // Но это сложный процесс, поэтому мы создадим временные InputActions
            CreateTemporaryInputActions();
        }

        /// <summary>
        /// Создание временных InputActions для тестирования
        /// </summary>
        private void CreateTemporaryInputActions()
        {
            var inputSystem = FindObjectOfType<Core.InputSystem3D>();
            if (inputSystem == null)
            {
                Debug.Log("[InputActionsSetup3D] InputSystem3D not found. Cannot create temporary actions.");
                return;
            }
            
            // Создаем InputActionAsset
            var inputActions = ScriptableObject.CreateInstance<InputActionAsset>();
            inputActions.name = "AutoCreated_InputActions";
            
            // Создаем Action Maps
            var playerMap = inputActions.AddActionMap("Player");
            var uiMap = inputActions.AddActionMap("UI");
            
            // Настройка Player Action Map
            SetupPlayerActions(playerMap);
            SetupUIActions(uiMap);
            
            // Назначаем InputActionAsset
            inputSystem.SetInputActions(inputActions);
            
            Debug.Log("[InputActionsSetup3D] Temporary Input Actions created successfully.");
        }

        /// <summary>
        /// Настройка действий игрока
        /// </summary>
        private void SetupPlayerActions(InputActionMap playerMap)
        {
            // Move (2D Vector)
            var moveAction = playerMap.AddAction("Move", InputActionType.Value, expectedControlLayout: "Vector2");
            moveAction.AddCompositeBinding("2DVector")
                .With("Up",    "<Keyboard>/w")
                .With("Down",  "<Keyboard>/s")
                .With("Left",  "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
            moveAction.AddBinding("<Gamepad>/leftStick");

            // Look (2D Vector)
            var lookAction = playerMap.AddAction("Look", InputActionType.Value, expectedControlLayout: "Vector2");
            lookAction.AddBinding("<Mouse>/delta");
            lookAction.AddBinding("<Gamepad>/rightStick");

            // Interact
            var interactAction = playerMap.AddAction("Interact", InputActionType.Button);
            interactAction.AddBinding("<Keyboard>/e");
            interactAction.AddBinding("<Gamepad>/buttonSouth");

            // Attack
            var attackAction = playerMap.AddAction("Attack", InputActionType.Button);
            attackAction.AddBinding("<Mouse>/leftButton");
            attackAction.AddBinding("<Gamepad>/buttonWest");

            // Sprint
            var sprintAction = playerMap.AddAction("Sprint", InputActionType.Button);
            sprintAction.AddBinding("<Keyboard>/leftShift");
            sprintAction.AddBinding("<Gamepad>/buttonNorth");

            // Crouch
            var crouchAction = playerMap.AddAction("Crouch", InputActionType.Button);
            crouchAction.AddBinding("<Keyboard>/leftCtrl");
            crouchAction.AddBinding("<Gamepad>/leftStickPress"); // South занят Interact

            // Jump
            var jumpAction = playerMap.AddAction("Jump", InputActionType.Button);
            jumpAction.AddBinding("<Keyboard>/space");
            jumpAction.AddBinding("<Gamepad>/buttonEast");

            // Previous
            var previousAction = playerMap.AddAction("Previous", InputActionType.Button);
            previousAction.AddBinding("<Keyboard>/q");
            previousAction.AddBinding("<Gamepad>/leftShoulder");

            // Next
            var nextAction = playerMap.AddAction("Next", InputActionType.Button);
            nextAction.AddBinding("<Keyboard>/tab");
            nextAction.AddBinding("<Gamepad>/rightShoulder");

            // DropDown (special move)
            var dropDownAction = playerMap.AddAction("DropDown", InputActionType.Button);
            dropDownAction.AddBinding("<Keyboard>/c");
            dropDownAction.AddBinding("<Gamepad>/dpad/down");
        }

        /// <summary>
        /// Настройка UI действий
        /// </summary>
        private void SetupUIActions(InputActionMap uiMap)
        {
            // Cancel/Pause
            var cancelAction = uiMap.AddAction("Cancel", InputActionType.Button);
            cancelAction.AddBinding("<Keyboard>/escape");
            cancelAction.AddBinding("<Gamepad>/start");
        }

        /// <summary>
        /// Создание InputActionAsset как скриптабельного объекта в проекте
        /// </summary>
        [ContextMenu("Create Input Actions Asset")]
        public void CreateInputActionsAssetFile()
        {
            // Этот метод создает InputActionAsset как файл в проекте
            // Для этого нужно использовать InputActionAsset Importer
            
            Debug.Log("[InputActionsSetup3D] To create Input Actions asset file, use Unity Editor's Input Actions creation tool.");
            Debug.Log("[InputActionsSetup3D] Window > Input System > Create Input Actions");
            
            // Показываем инструкции
            ShowInputActionsCreationInstructions();
        }

        /// <summary>
        /// Показ инструкций по созданию Input Actions
        /// </summary>
        private void ShowInputActionsCreationInstructions()
        {
            Debug.Log("=== Input Actions Setup Instructions ===");
            Debug.Log("1. Open Window > Input System > Create Input Actions");
            Debug.Log("2. Click 'Create Input Actions' button");
            Debug.Log("3. Save as: Assets/InputSystem_Actions.inputactions");
            Debug.Log("4. Configure actions as follows:");
            Debug.Log("   - Player/ActionMap:");
            Debug.Log("     * Move: WASD, Gamepad/leftStick");
            Debug.Log("     * Look: Mouse/delta, Gamepad/rightStick");
            Debug.Log("     * Interact: E, Gamepad/btnSouth");
            Debug.Log("     * Attack: Left Mouse, Gamepad/btnWest");
            Debug.Log("     * Sprint: Left Shift, Gamepad/btnNorth");
            Debug.Log("     * Crouch: Left Ctrl");
            Debug.Log("     * Jump: Space, Gamepad/btnEast");
            Debug.Log("     * Previous: Q");
            Debug.Log("     * Next: Tab");
            Debug.Log("     * DropDown: C");
            Debug.Log("   - UI/ActionMap:");
            Debug.Log("     * Cancel: Escape, Gamepad/btnSelect");
            Debug.Log("5. Assign the asset to InputSystem3D component");
            Debug.Log("============================================");
        }

        /// <summary>
        /// Проверка всех необходимых Input Actions
        /// </summary>
        public bool ValidateInputActions()
        {
            var inputSystem = FindObjectOfType<Core.InputSystem3D>();
            if (inputSystem == null || inputSystem.InputActions == null)
            {
                Debug.LogError("[InputActionsSetup3D] InputSystem3D or InputActions not configured!");
                return false;
            }
            
            var playerMap = inputSystem.InputActions.FindActionMap("Player");
            var uiMap = inputSystem.InputActions.FindActionMap("UI");
            
            if (playerMap == null)
            {
                Debug.LogError("[InputActionsSetup3D] Player ActionMap not found!");
                return false;
            }
            
            // Проверяем обязательные действия
            var requiredActions = new string[] { "Move", "Interact", "Attack", "Sprint" };
            foreach (var actionName in requiredActions)
            {
                var action = playerMap.FindAction(actionName);
                if (action == null)
                {
                    Debug.LogError($"[InputActionsSetup3D] Required action '{actionName}' not found in Player ActionMap!");
                    return false;
                }
            }
            
            Debug.Log("[InputActionsSetup3D] All required input actions are configured correctly!");
            return true;
        }

        /// <summary>
        /// Ручная настройка Input Actions
        /// </summary>
        [ContextMenu("Setup Input Actions Manually")]
        public void SetupInputActionsManually()
        {
            CreateTemporaryInputActions();
        }
    }
}