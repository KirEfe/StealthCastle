using System.Collections.Generic;
using StealthCastle.Isometric.Player;
using StealthCastle.Isometric.Stealth;
using UnityEngine;
using UnityEngine.InputSystem;

namespace StealthCastle.Isometric.Mechanics
{
    /// <summary>
    /// Маскировка в предметы окружения (этап 2, изометрия). Адаптация StealthCastle.Mechanics.DisguiseSystem
    /// под прототип: зажатие Interact → скан радиуса → подсветка пропов → превращение (отпускание).
    /// Реализует IStealthTarget для врагов.
    /// </summary>
    [RequireComponent(typeof(IsoPlayerDisguiseVisual))]
    public class IsoDisguiseSystem : MonoBehaviour, IStealthTarget
    {
        enum DisguisePhase
        {
            Idle,
            Scanning,
            Disguised
        }

        [Header("Тайминг")]
        [SerializeField] private float disguiseDuration = 6f;  // таймер дыхания, сек
        [SerializeField] private float scanRadius = 2f;       // радиус скана предметов, м

        [Header("Слой маскировки")]
        [SerializeField] private string disguisedLayerName = "DisguisedPlayer";

        private DisguisePhase phase = DisguisePhase.Idle;
        private float phaseTimer;

        private IsoPlayerDisguiseVisual disguiseVisual;
        private IsoPlayerController playerController;
        private InputSystem_Actions inputActions; // wrapper для корректного Dispose
        private InputAction interactAction;

        private readonly List<IsoDisguisableProp> candidates = new();
        private int selectedIndex = -1;
        private IsoDisguisableProp activeProp;
        private Sprite activeDisguiseSprite;

        private int originalPlayerLayer;
        private int disguisedPlayerLayer;
        private bool layerSwitched;

        public bool IsDisguised => phase == DisguisePhase.Disguised;
        public Sprite CurrentDisguiseSprite => activeDisguiseSprite;
        public bool CanBeDetected() => !IsDisguised;

        // Физическая адаптация: метод на IsoPlayerController для коллайдера
        public bool IsMoving => playerController != null && playerController.IsMoving();

        private void Awake()
        {
            disguiseVisual = GetComponent<IsoPlayerDisguiseVisual>();
            playerController = GetComponent<IsoPlayerController>();

            // Кэшируем слои
            originalPlayerLayer = gameObject.layer;
            disguisedPlayerLayer = LayerMask.NameToLayer(disguisedLayerName);
            if (disguisedPlayerLayer == -1)
                Debug.LogError($"[IsoDisguiseSystem] Слой '{disguisedLayerName}' не найден! Создай его в Project Settings.");

            // Ввод через существующий экшен Player/Interact (Hold)
            inputActions = new InputSystem_Actions();
            interactAction = inputActions.Player.Interact;
            interactAction.Enable();
        }

        private void OnDestroy()
        {
            inputActions?.Dispose();
        }

        private void Update()
        {
            if (interactAction == null) return;

            switch (phase)
            {
                case DisguisePhase.Idle:
                    if (interactAction.IsPressed()) BeginScanning();
                    break;

                case DisguisePhase.Scanning:
                    UpdateScanning();
                    break;

                case DisguisePhase.Disguised:
                    UpdateDisguised();
                    break;
            }
        }

        // Начало скана при зажатии Interact
        private void BeginScanning()
        {
            RefreshCandidates();
            if (candidates.Count == 0)
            {
                Debug.Log("[IsoDisguise] Рядом нет подходящих предметов.");
                return;
            }

            phase = DisguisePhase.Scanning;
            selectedIndex = 0;
            UpdateHighlights();
            Debug.Log($"[IsoDisguise] Скан: {candidates.Count} пропов. Отпусти Interact — превратиться в ближайший.");
        }

        private void UpdateScanning()
        {
            // Отпускание Interact → превращение в выбранный (ближайший по умолчанию)
            if (interactAction.WasReleasedThisFrame())
            {
                if (candidates.Count > 0 && selectedIndex >= 0 && selectedIndex < candidates.Count)
                    ConfirmDisguise(candidates[selectedIndex]);
                else
                    CancelToIdle();
                return;
            }

            // Обновляем кандидатов (игрок мог сдвинуться)
            RefreshCandidates();
            if (candidates.Count == 0)
            {
                CancelToIdle();
                return;
            }
            if (selectedIndex >= candidates.Count) selectedIndex = candidates.Count - 1;

            UpdateHighlights();
        }

        private void UpdateDisguised()
        {
            phaseTimer -= Time.deltaTime;
            if (phaseTimer <= 0f)
            {
                RemoveDisguise("Время маскировки истекло.");
                return;
            }

            // Повторное нажатие Interact → снятие
            if (interactAction.WasPressedThisFrame())
                RemoveDisguise("Маскировка снята вручную.");
        }

        private void ConfirmDisguise(IsoDisguisableProp prop)
        {
            if (prop == null) return;

            activeProp = prop;
            var source = prop.Renderer;
            disguiseVisual.ApplyDisguise(source);
            activeDisguiseSprite = source != null ? source.sprite : null;

            ClearHighlights();
            candidates.Clear();
            selectedIndex = -1;

            phase = DisguisePhase.Disguised;
            phaseTimer = disguiseDuration;

            // Переключаем физический слой
            if (disguisedPlayerLayer != -1)
            {
                gameObject.layer = disguisedPlayerLayer;
                layerSwitched = true;
            }

            Debug.Log($"[IsoDisguise] Замаскирован под: {prop.name}. Враги не обнаружат (неподвижного).");
        }

        public void RemoveDisguise(string reason)
        {
            Debug.Log($"[IsoDisguise] {reason}");

            if (layerSwitched && originalPlayerLayer != -1)
                gameObject.layer = originalPlayerLayer;
            layerSwitched = false;

            disguiseVisual.ClearDisguise();
            activeDisguiseSprite = null;
            activeProp = null;
            phase = DisguisePhase.Idle;
            phaseTimer = 0f;
        }

        private void RefreshCandidates()
        {
            candidates.Clear();
            var origin = (Vector2)transform.position;
            var props = FindObjectsByType<IsoDisguisableProp>(FindObjectsSortMode.None);

            foreach (var prop in props)
            {
                if (prop.gameObject == gameObject) continue;
                var collider = prop.GetComponent<Collider2D>();
                float distance = collider != null
                    ? Vector2.Distance(origin, collider.ClosestPoint(origin))
                    : Vector2.Distance(origin, prop.transform.position);

                if (distance <= scanRadius) candidates.Add(prop);
            }

            // Сортировка по близости — ближайший первый
            candidates.Sort((a, b) =>
                Vector2.Distance(origin, a.transform.position)
                .CompareTo(Vector2.Distance(origin, b.transform.position)));
        }

        private void UpdateHighlights()
        {
            foreach (var prop in candidates)
                prop.SetHighlighted(true);
        }

        private void ClearHighlights()
        {
            foreach (var prop in candidates)
                prop.SetHighlighted(false);
        }

        private void CancelToIdle()
        {
            ClearHighlights();
            candidates.Clear();
            selectedIndex = -1;
            phase = DisguisePhase.Idle;
            phaseTimer = 0f;
        }

        /// <summary>Оставшееся время дыхания (для HUD).</summary>
        public float RemainingTime => Mathf.Max(0f, phaseTimer);

        /// <summary>Полное время дыхания (для HUD).</summary>
        public float TotalTime => disguiseDuration;
    }
}
