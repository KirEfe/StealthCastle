using System.Collections.Generic;
using StealthCastle.Player;
using StealthCastle.Stealth;
using UnityEngine;
using UnityEngine.InputSystem;

namespace StealthCastle.Mechanics
{
    [RequireComponent(typeof(PlayerDisguiseVisual))]
    public class DisguiseSystem : MonoBehaviour, IStealthTarget
    {
        enum DisguisePhase
        {
            Idle,
            BreathHold,
            Scanning,
            Disguised
        }

        [Header("Timing")]
        [SerializeField] float breathHoldDelay = 0.3f;
        [SerializeField] float selectionDuration = 5f;
        [SerializeField] float disguiseDuration = 5f;

        [Header("Scanning")]
        [SerializeField] float scanRadius = 2f;
        [SerializeField] LayerMask clickableLayers = ~0;

        [Header("Input")]
        [SerializeField] InputActionAsset inputActions;

        [Header("Debug")]
        [SerializeField] bool drawScanRadius = true;

        InputAction interactAction;
        InputAction previousAction;
        InputAction nextAction;
        InputAction confirmAction;

        PlayerDisguiseVisual disguiseVisual;
        PlayerController playerController;
        DisguisePhase phase = DisguisePhase.Idle;

        float phaseTimer;
        readonly List<DisguisableProp> candidates = new();
        int selectedIndex = -1;
        Sprite activeDisguiseSprite;

        public bool IsDisguised => phase == DisguisePhase.Disguised;
        public Sprite CurrentDisguiseSprite => activeDisguiseSprite;

        public bool CanBeDetected() => !IsDisguised;

        void Awake()
        {
            disguiseVisual = GetComponent<PlayerDisguiseVisual>();
            playerController = GetComponent<PlayerController>();
            BindInputActions();
        }

        void OnEnable()
        {
            inputActions?.Enable();
        }

        void OnDisable()
        {
            inputActions?.Disable();
            ClearHighlights();
        }

        void BindInputActions()
        {
            if (inputActions == null)
            {
                Debug.LogError($"{nameof(DisguiseSystem)} requires an InputActionAsset.", this);
                return;
            }

            var playerMap = inputActions.FindActionMap("Player", true);
            interactAction = playerMap.FindAction("Interact", true);
            previousAction = playerMap.FindAction("Previous", true);
            nextAction = playerMap.FindAction("Next", true);
            confirmAction = playerMap.FindAction("Attack", true);
        }

        void Update()
        {
            switch (phase)
            {
                case DisguisePhase.Idle:
                    TryBeginBreathHold();
                    break;
                case DisguisePhase.BreathHold:
                    UpdateBreathHold();
                    break;
                case DisguisePhase.Scanning:
                    UpdateScanning();
                    break;
                case DisguisePhase.Disguised:
                    UpdateDisguised();
                    break;
            }
        }

        void TryBeginBreathHold()
        {
            if (!IsInteractHeld())
                return;

            phase = DisguisePhase.BreathHold;
            phaseTimer = breathHoldDelay;
            Debug.Log("[Disguise] Задержка дыхания...");
        }

        void UpdateBreathHold()
        {
            if (!IsInteractHeld())
            {
                CancelToIdle("Дыхание прервано.");
                return;
            }

            phaseTimer -= Time.deltaTime;
            if (phaseTimer > 0f)
                return;

            BeginScanning();
        }

        void BeginScanning()
        {
            RefreshCandidates();

            if (candidates.Count == 0)
            {
                CancelToIdle("Рядом нет подходящих предметов для маскировки.");
                return;
            }

            phase = DisguisePhase.Scanning;
            phaseTimer = selectionDuration;
            selectedIndex = 0;
            UpdateHighlights();

            Debug.Log($"[Disguise] Выбор маскировки: {selectionDuration:0.#} сек. ЛКМ или Enter — подтвердить, ←/→ — переключить.");
        }

        void UpdateScanning()
        {
            if (!IsInteractHeld())
            {
                CancelToIdle("Маскировка отменена.");
                return;
            }

            phaseTimer -= Time.deltaTime;
            if (phaseTimer <= 0f)
            {
                CancelToIdle("Время выбора истекло.");
                return;
            }

            RefreshCandidates();

            if (candidates.Count == 0)
            {
                CancelToIdle("Подходящие предметы больше не рядом.");
                return;
            }

            if (selectedIndex >= candidates.Count)
                selectedIndex = candidates.Count - 1;

            // Приоритет наведению мыши
            int mouseIndex = GetCandidateIndexUnderMouse();
            if (mouseIndex != -1)
            {
                selectedIndex = mouseIndex;
            }
            else
            {
                HandleSelectionInput();
            }

            UpdateHighlights();

            if (WasConfirmPressed() || (WasMousePressedThisFrame() && selectedIndex != -1))
                ConfirmDisguise();
        }

        void HandleSelectionInput()
        {
            if (WasPreviousPressed())
            {
                selectedIndex = (selectedIndex - 1 + candidates.Count) % candidates.Count;
            }
            else if (WasNextPressed())
            {
                selectedIndex = (selectedIndex + 1) % candidates.Count;
            }
        }

        int GetCandidateIndexUnderMouse()
        {
            var camera = Camera.main;
            if (camera == null)
                return -1;

            var screenPoint = Mouse.current.position.ReadValue();
            var worldPoint = camera.ScreenToWorldPoint(new Vector3(screenPoint.x, screenPoint.y, 0f - camera.transform.position.z));
            var hit = Physics2D.OverlapPoint(worldPoint, clickableLayers);
            if (hit == null)
                return -1;

            var prop = hit.GetComponent<DisguisableProp>();
            if (prop == null)
                return -1;

            return candidates.IndexOf(prop);
        }

        void ConfirmDisguise()
        {
            if (selectedIndex < 0 || selectedIndex >= candidates.Count)
                return;

            var selectedProp = candidates[selectedIndex];
            var sourceRenderer = selectedProp.SpriteRenderer;

            // 1. Меняем визуал
            disguiseVisual.ApplyDisguise(sourceRenderer);
            activeDisguiseSprite = sourceRenderer != null ? sourceRenderer.sprite : null;

            // 2. АДАПТИРУЕМ ФИЗИКУ: Копируем размеры коллайдера предмета
            var propCollider = selectedProp.GetComponent<Collider2D>();
            var playerController = GetComponent<PlayerController>();
            if (propCollider != null && playerController != null)
            {
                // Передаем размеры из bounds (мировые габариты) и offset пропса
                playerController.AdaptColliderToDisguise(propCollider.bounds.size, propCollider.offset);
            }

            ClearHighlights();
            candidates.Clear();
            selectedIndex = -1;

            phase = DisguisePhase.Disguised;
            phaseTimer = disguiseDuration;
            Debug.Log($"[Disguise] Замаскирован под: {selectedProp.DisplayName}. Враги не обнаружат.");
        }

        void UpdateDisguised()
        {
            phaseTimer -= Time.deltaTime;
            if (phaseTimer <= 0f)
            {
                RemoveDisguise("Время маскировки истекло.");
                return;
            }

            if (WasInteractPressed())
            {
                RemoveDisguise("Маскировка снята вручную.");
            }
        }

        public void RemoveDisguise(string reason)
        {
            Debug.Log($"[Disguise] {reason}");
            disguiseVisual.ClearDisguise();
            activeDisguiseSprite = null;
            phase = DisguisePhase.Idle;
            phaseTimer = 0f;

            // 3. ВОЗВРАЩАЕМ ФИЗИКУ: Возвращаем вору его родной коллайдер
            var playerController = GetComponent<PlayerController>();
            if (playerController != null)
            {
                playerController.ResetColliderToNormal();
            }
        }

        void RefreshCandidates()
        {
            candidates.Clear();

            var origin = (Vector2)transform.position;
            var props = FindObjectsByType<DisguisableProp>(FindObjectsSortMode.None);

            foreach (var prop in props)
            {
                if (prop.gameObject == gameObject)
                    continue;

                if (prop.CompareTag("Enemy"))
                    continue;

                var collider = prop.GetComponent<Collider2D>();
                float distance;
                if (collider != null)
                {
                    var closestPoint = collider.ClosestPoint(origin);
                    distance = Vector2.Distance(origin, closestPoint);
                }
                else
                {
                    distance = Vector2.Distance(origin, prop.transform.position);
                }

                if (distance <= scanRadius)
                    candidates.Add(prop);
            }

            candidates.Sort((a, b) =>
            {
                var collA = a.GetComponent<Collider2D>();
                var collB = b.GetComponent<Collider2D>();
                var distA = collA != null
                    ? Vector2.Distance(origin, collA.ClosestPoint(origin))
                    : Vector2.Distance(origin, a.transform.position);
                var distB = collB != null
                    ? Vector2.Distance(origin, collB.ClosestPoint(origin))
                    : Vector2.Distance(origin, b.transform.position);
                return distA.CompareTo(distB);
            });
        }

        void UpdateHighlights()
        {
            var props = FindObjectsByType<DisguisableProp>(FindObjectsSortMode.None);

            foreach (var prop in props)
            {
                if (!prop.TryGetComponent<DisguiseHighlight>(out var highlight))
                    continue;

                var candidateIndex = candidates.IndexOf(prop);
                var isCandidate = candidateIndex >= 0;
                var isSelected = candidateIndex == selectedIndex;

                highlight.SetHighlight(isCandidate, isSelected);
            }
        }

        void ClearHighlights()
        {
            var highlights = FindObjectsByType<DisguiseHighlight>(FindObjectsSortMode.None);
            foreach (var highlight in highlights)
                highlight.SetHighlight(false, false);
        }

        void CancelToIdle(string reason)
        {
            Debug.Log($"[Disguise] {reason}");
            ClearHighlights();
            candidates.Clear();
            selectedIndex = -1;
            phase = DisguisePhase.Idle;
            phaseTimer = 0f;
        }

        bool IsInteractHeld()
        {
            if (interactAction == null)
                return false;

            foreach (var control in interactAction.controls)
            {
                if (control.IsPressed())
                    return true;
            }

            return false;
        }

        bool WasPreviousPressed() => previousAction != null && previousAction.WasPressedThisFrame();

        bool WasNextPressed() => nextAction != null && nextAction.WasPressedThisFrame();

        bool WasConfirmPressed() => confirmAction != null && confirmAction.WasPressedThisFrame();

        bool WasInteractPressed()
        {
            if (interactAction == null) return false;
            return interactAction.WasPressedThisFrame();
        }

        bool WasMousePressedThisFrame() =>
            Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;

        void OnDrawGizmosSelected()
        {
            if (!drawScanRadius)
                return;

            Gizmos.color = new Color(1f, 0.92f, 0.016f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, scanRadius);
        }
    }
}
