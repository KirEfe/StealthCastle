using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace StealthCastle.Isometric3D.Mechanics
{
    /// <summary>
    /// Унифицированная система маскировки для 3D изометрической версии.
    /// Объединяет лучшее из DisguiseSystem (2D) и IsoDisguiseSystem (изометрия).
    /// </summary>
    public class DisguiseSystem3D : MonoBehaviour
    {
        public enum DisguisePhase
        {
            Idle,
            Scanning,
            Disguised
        }

        [Header("Timing Settings")]
        [SerializeField] private float disguiseDuration = 6f; // Время маскировки
        [SerializeField] private bool allowCrawling = true; // GDD: медленное движение в маскировке разрешено
        [SerializeField] private float scanRadius = 2f; // Радиус сканирования предметов

        [Header("Selection Settings")]
        [SerializeField] private float selectionDuration = 5f; // Время выбора предмета
        [SerializeField] private float scanInterval = 0.5f; // Интервал сканирования

        [Header("Layer Settings")]
        [SerializeField] private string disguisedLayerName = "DisguisedPlayer";

        [Header("Debug")]
        [SerializeField] private bool drawScanRadius = true;

        private DisguisePhase phase = DisguisePhase.Idle;
        private float phaseTimer;
        private int originalPlayerLayer;
        private int disguisedPlayerLayer;
        private bool layerSwitched;

        private Player.PlayerController3D playerController;
        private Player.PlayerDisguiseVisual3D disguiseVisual;
        private Core.InputSystem3D inputSystem;

        private readonly List<DisguisableProp3D> candidates = new List<DisguisableProp3D>();
        private int selectedIndex = -1;
        private DisguisableProp3D activeProp;

        private float scanTimer;

        /// <summary>Замаскирован ли игрок</summary>
        public bool IsDisguised => phase == DisguisePhase.Disguised;

        /// <summary>Движется ли игрок (для обнаружения ползущего врагом)</summary>
        public bool IsMoving => playerController != null && playerController.IsMoving;

        /// <summary>Может ли быть обнаружен</summary>
        public bool CanBeDetected() => !IsDisguised;

        /// <summary>Оставшееся время маскировки</summary>
        public float RemainingTime => Mathf.Max(0f, phaseTimer);

        /// <summary>Полное время маскировки</summary>
        public float TotalTime => disguiseDuration;

        /// <summary>Радиус поиска предметов для маскировки</summary>
        public float ScanRadius => scanRadius;

        private void Awake()
        {
            playerController = GetComponent<Player.PlayerController3D>();
            disguiseVisual = GetComponent<Player.PlayerDisguiseVisual3D>();
            inputSystem = FindObjectOfType<Core.InputSystem3D>();

            // Кэшируем слои
            originalPlayerLayer = gameObject.layer;
            disguisedPlayerLayer = LayerMask.NameToLayer(disguisedLayerName);
            
            if (disguisedPlayerLayer == -1)
            {
                Debug.LogError($"[DisguiseSystem3D] Layer '{disguisedLayerName}' not found! Create it in Project Settings -> Tags & Layers.");
            }
        }

        private void OnEnable()
        {
            if (inputSystem != null)
            {
                inputSystem.SetInputEnabled(true);
            }
        }

        private void OnDisable()
        {
            if (inputSystem != null)
            {
                inputSystem.SetInputEnabled(false);
            }
        }

        private void Update()
        {
            switch (phase)
            {
                case DisguisePhase.Idle:
                    TryBeginScanning();
                    break;
                
                case DisguisePhase.Scanning:
                    UpdateScanning();
                    break;
                
                case DisguisePhase.Disguised:
                    UpdateDisguised();
                    break;
            }
        }

        /// <summary>
        /// Попытка начать сканирование
        /// </summary>
        private void TryBeginScanning()
        {
            if (inputSystem == null) return;

            // Начало сканирования при зажатии Interact
            if (inputSystem.InteractHeld)
            {
                scanTimer += Time.deltaTime;
                
                // Периодическое сканирование
                if (scanTimer >= scanInterval)
                {
                    scanTimer = 0f;
                    RefreshCandidates();
                }

                // Если есть кандидаты, начинаем выбор
                if (candidates.Count > 0)
                {
                    BeginSelection();
                }
            }
            else
            {
                scanTimer = 0f;
            }
        }

        /// <summary>
        /// Начало выбора предмета
        /// </summary>
        private void BeginSelection()
        {
            if (candidates.Count == 0) return;

            phase = DisguisePhase.Scanning;
            phaseTimer = selectionDuration;
            selectedIndex = 0;
            UpdateHighlights();
            
            Debug.Log($"[DisguiseSystem3D] Scanning: {candidates.Count} props found. Release Interact to disguise.");
        }

        /// <summary>
        /// Обновление фазы сканирования
        /// </summary>
        private void UpdateScanning()
        {
            // Отпускание Interact - маскировка в выбранный предмет
            if (inputSystem != null && inputSystem.InteractReleased)
            {
                if (selectedIndex >= 0 && selectedIndex < candidates.Count)
                {
                    ConfirmDisguise(candidates[selectedIndex]);
                }
                else
                {
                    CancelToIdle();
                }
                return;
            }

            // Обновляем кандидатов
            scanTimer += Time.deltaTime;
            if (scanTimer >= scanInterval)
            {
                scanTimer = 0f;
                RefreshCandidates();
                
                if (candidates.Count == 0)
                {
                    CancelToIdle();
                    return;
                }
            }

            // Навигация по кандидатам
            if (inputSystem != null)
            {
                if (inputSystem.PreviousPressed)
                {
                    selectedIndex = (selectedIndex - 1 + candidates.Count) % candidates.Count;
                    UpdateHighlights();
                }
                else if (inputSystem.NextPressed)
                {
                    selectedIndex = (selectedIndex + 1) % candidates.Count;
                    UpdateHighlights();
                }
            }

            // Таймаут выбора
            phaseTimer -= Time.deltaTime;
            if (phaseTimer <= 0f)
            {
                CancelToIdle();
            }
        }

        /// <summary>
        /// Обновление фазы маскировки
        /// </summary>
        private void UpdateDisguised()
        {
            phaseTimer -= Time.deltaTime;
            if (phaseTimer <= 0f)
            {
                RemoveDisguise("Disguise time expired.");
                return;
            }

            // Снятие маскировки по нажатию
            if (inputSystem != null && inputSystem.InteractPressed)
            {
                RemoveDisguise("Disguise removed manually.");
            }

            // Снятие маскировки при движении — только если ползание выключено
            if (!allowCrawling && IsMoving)
            {
                RemoveDisguise("Disguise broken by movement.");
            }
        }

        /// <summary>
        /// Подтверждение маскировки
        /// </summary>
        private void ConfirmDisguise(DisguisableProp3D prop)
        {
            if (prop == null) return;

            activeProp = prop;

            // Сначала снимаем подсветку: иначе визуал скопирует материал подсветки вместо материала пропа
            ClearHighlights();

            // Применяем визуал маскировки
            if (disguiseVisual != null)
            {
                disguiseVisual.ApplyDisguise(prop);
            }

            // Занятие пропа
            prop.SetOccupied(true);

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

            Debug.Log($"[DisguiseSystem3D] Disguised as: {prop.name}. Enemies won't detect (if stationary).");
        }

        /// <summary>
        /// Снятие маскировки
        /// </summary>
        public void RemoveDisguise(string reason)
        {
            Debug.Log($"[DisguiseSystem3D] {reason}");

            // Возвращаем физический слой
            if (layerSwitched && originalPlayerLayer != -1)
            {
                gameObject.layer = originalPlayerLayer;
            }
            layerSwitched = false;

            // Очищаем визуал
            if (disguiseVisual != null)
            {
                disguiseVisual.ClearDisguise();
            }

            // Освобождаем проп
            if (activeProp != null)
            {
                activeProp.SetOccupied(false);
                activeProp = null;
            }

            phase = DisguisePhase.Idle;
            phaseTimer = 0f;
        }

        /// <summary>
        /// Обновление подсветки кандидатов
        /// </summary>
        private void UpdateHighlights()
        {
            foreach (var prop in candidates)
            {
                if (prop != null)
                {
                    bool isSelected = candidates.IndexOf(prop) == selectedIndex;
                    prop.SetHighlighted(true, isSelected);
                }
            }
        }

        /// <summary>
        /// Очистка подсветки
        /// </summary>
        private void ClearHighlights()
        {
            foreach (var prop in candidates)
            {
                if (prop != null)
                {
                    prop.SetHighlighted(false, false);
                }
            }
        }

        /// <summary>
        /// Отмена и возвращение в состояние покоя
        /// </summary>
        private void CancelToIdle()
        {
            ClearHighlights();
            candidates.Clear();
            selectedIndex = -1;
            phase = DisguisePhase.Idle;
            phaseTimer = 0f;
            scanTimer = 0f;
        }

        /// <summary>
        /// Обновление списка кандидатов
        /// </summary>
        private void RefreshCandidates()
        {
            candidates.Clear();

            var origin = transform.position;
            var props = FindObjectsByType<DisguisableProp3D>(FindObjectsSortMode.None);

            foreach (var prop in props)
            {
                if (prop.gameObject == gameObject) continue;
                if (prop.IsOccupied()) continue;

                var collider = prop.GetComponent<Collider>();
                float distance;
                if (collider != null)
                {
                    var closestPoint = collider.ClosestPoint(origin);
                    distance = Vector3.Distance(origin, closestPoint);
                }
                else
                {
                    distance = Vector3.Distance(origin, prop.transform.position);
                }

                if (distance <= scanRadius)
                {
                    candidates.Add(prop);
                }
            }

            // Сортировка по расстоянию
            candidates.Sort((a, b) =>
            {
                float distA = Vector3.Distance(origin, a.transform.position);
                float distB = Vector3.Distance(origin, b.transform.position);
                return distA.CompareTo(distB);
            });

            // Обновляем выбранный индекс
            if (selectedIndex >= candidates.Count)
            {
                selectedIndex = candidates.Count - 1;
            }
        }

        /// <summary>
        /// Установка времени маскировки
        /// </summary>
        public void SetDisguiseDuration(float duration)
        {
            disguiseDuration = duration;
        }

        /// <summary>
        /// Установка радиуса сканирования
        /// </summary>
        public void SetScanRadius(float radius)
        {
            scanRadius = radius;
        }

        /// <summary>
        /// Принудительное снятие маскировки
        /// </summary>
        public void ForceRemoveDisguise()
        {
            if (IsDisguised)
            {
                RemoveDisguise("Forced disguise removal.");
            }
        }

        /// <summary>
        /// Отладочная визуализация
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            if (!drawScanRadius) return;

            // Радиус сканирования
            Gizmos.color = new Color(1f, 0.92f, 0.016f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, scanRadius);

            // Текущий выбранный предмет
            if (selectedIndex >= 0 && selectedIndex < candidates.Count && candidates[selectedIndex] != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(candidates[selectedIndex].transform.position, 0.5f);
            }
        }
    }
}