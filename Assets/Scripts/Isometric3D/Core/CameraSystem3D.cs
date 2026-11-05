using UnityEngine;
using UnityEngine.InputSystem;

namespace StealthCastle.Isometric3D.Core
{
    /// <summary>
    /// Система камеры для 3D изометрической игры.
    /// Поддерживает классический изометрический вид и дополнительные функции.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class CameraSystem3D : MonoBehaviour
    {
        [Header("Target Settings")]
        [SerializeField] private Transform target; // Целевой объект (игрок)
        [SerializeField] private float smoothTime = 0.15f; // Время сглаживания
        [SerializeField] private Vector3 lookAtOffset = new Vector3(0f, 0.5f, 0f); // Точка взгляда относительно цели

        [Header("Isometric Settings")]
        [SerializeField] private float rotationAngle = 35f; // Угол наклона изометрической камеры (0-90)
        [SerializeField] private float zoomDistance = 20f; // Дистанция до цели
        [SerializeField] private float minZoom = 5f; // Минимальный зум
        [SerializeField] private float maxZoom = 30f; // Максимальный зум
        [SerializeField] private float zoomSpeed = 5f; // Скорость зума
        [SerializeField] private float rotationSpeed = 2f; // Скорость вращения

        [Header("Projection Settings")]
        [SerializeField] private bool useOrthographic = false; // Использовать ортографическую проекцию
        [SerializeField] private float orthographicSize = 10f; // Размер ортографической камеры
        [SerializeField] private float fieldOfView = 60f; // Угол обзора для перспективы

        [Header("Transparency Sorting")]
        [SerializeField] private TransparencySortMode sortMode = TransparencySortMode.CustomAxis;
        [SerializeField] private Vector3 sortAxis = new Vector3(0f, 1f, 0f); // Ось сортировки по Y

        [Header("Level Overview")]
        [SerializeField] private float overviewTransitionTime = 1.2f; // Отъезд к общему плану и возврат, с
        [SerializeField] private float overviewPadding = 1.05f; // Запас по краям общего плана

        private Camera cameraComponent;
        private Vector3 velocity;
        private float currentZoom;
        private float currentRotationX;
        private float currentRotationY;

        // Общий план уровня: 0 — камера у игрока, 1 — весь уровень в кадре
        private bool overviewActive;
        private float overviewBlend;
        private Vector3 overviewCenter;
        private float overviewDistance;

        /// <summary>Текущая камера</summary>
        public Camera Camera => cameraComponent;

        /// <summary>Длительность перехода к общему плану и обратно, с</summary>
        public float OverviewTransitionTime => overviewTransitionTime;

        /// <summary>Общий план показан или камера ещё не вернулась к игроку</summary>
        public bool IsOverview => overviewActive || overviewBlend > 0.5f;

        /// <summary>
        /// Показать уровень целиком: тот же изометрический угол, камера отъезжает так, чтобы границы влезли в кадр
        /// </summary>
        public void ShowOverview(Bounds levelBounds)
        {
            overviewCenter = levelBounds.center;
            overviewDistance = FitDistance(levelBounds);
            overviewActive = true;
        }

        /// <summary>Вернуть камеру к игроку (плавно, за OverviewTransitionTime)</summary>
        public void HideOverview()
        {
            overviewActive = false;
        }

        /// <summary>
        /// Дистанция, на которой сфера вокруг границ уровня целиком влезает в кадр по обеим осям
        /// </summary>
        private float FitDistance(Bounds bounds)
        {
            float radius = bounds.extents.magnitude * overviewPadding;
            float verticalHalf = cameraComponent.fieldOfView * 0.5f * Mathf.Deg2Rad;
            float horizontalHalf = Mathf.Atan(Mathf.Tan(verticalHalf) * cameraComponent.aspect);
            return radius / Mathf.Sin(Mathf.Min(verticalHalf, horizontalHalf));
        }

        /// <summary>Целевой объект</summary>
        public Transform Target
        {
            get => target;
            set
            {
                target = value;
                if (target != null)
                {
                    currentZoom = zoomDistance;
                    CalculateInitialRotation();
                }
            }
        }

        private void Awake()
        {
            cameraComponent = GetComponent<Camera>();
            currentZoom = zoomDistance;
            
            // Настройка проекции
            UpdateProjection();
            
            // Настройка сортировки прозрачности
            UpdateTransparencySorting();
        }

        private void Start()
        {
            // Вычисляем начальное вращение
            CalculateInitialRotation();

            // Без плавного 'подлёта' из начальной позиции
            if (target != null)
            {
                UpdateCameraRotation();
                transform.position = GetDesiredPosition();
            }
        }

        /// <summary>
        /// Вычисление начального вращения камеры
        /// </summary>
        private void CalculateInitialRotation()
        {
            // Угол по X (наклон вниз)
            currentRotationX = rotationAngle;
            
            // Угол по Y (поворот вокруг вертикальной оси)
            currentRotationY = 45f; // Классический изометрический угол
        }

        /// <summary>
        /// Настройка проекции камеры
        /// </summary>
        private void UpdateProjection()
        {
            cameraComponent.orthographic = useOrthographic;
            
            if (useOrthographic)
            {
                cameraComponent.orthographicSize = orthographicSize;
            }
            else
            {
                cameraComponent.fieldOfView = fieldOfView;
            }
        }

        /// <summary>
        /// Настройка сортировки прозрачности
        /// </summary>
        private void UpdateTransparencySorting()
        {
            cameraComponent.transparencySortMode = sortMode;
            cameraComponent.transparencySortAxis = sortAxis;
        }

        private void LateUpdate()
        {
            if (target == null) return;

            // Обработка ввода для зума и вращения
            HandleInput();
            
            // Вычисление позиции камеры
            UpdateCameraPosition();
            
            // Вычисление вращения камеры
            UpdateCameraRotation();
        }

        /// <summary>
        /// Обработка ввода от пользователя
        /// </summary>
        private void HandleInput()
        {
            // Проект использует только Input System — legacy Input бросает исключение
            var mouse = Mouse.current;
            if (mouse == null) return;

            // Зум колесиком мыши (знак, т.к. шаг колеса зависит от платформы)
            float scrollWheel = mouse.scroll.ReadValue().y;
            if (scrollWheel != 0)
            {
                currentZoom -= Mathf.Sign(scrollWheel) * zoomSpeed * 0.1f;
                currentZoom = Mathf.Clamp(currentZoom, minZoom, maxZoom);
            }

            // Вращение камеры (опционально)
            if (mouse.middleButton.isPressed) // Средняя кнопка мыши
            {
                // 0.1 — чувствительность, как у legacy "Mouse X/Y"
                Vector2 mouseDelta = mouse.delta.ReadValue() * 0.1f;

                currentRotationY += mouseDelta.x * rotationSpeed;
                currentRotationX -= mouseDelta.y * rotationSpeed;
                
                // Ограничиваем углы вращения
                currentRotationX = Mathf.Clamp(currentRotationX, 10f, 80f);
            }
        }

        /// <summary>
        /// Обновление позиции камеры
        /// </summary>
        private void UpdateCameraPosition()
        {
            // Переход к общему плану — по реальному времени, чтобы не зависеть от паузы
            float targetBlend = overviewActive ? 1f : 0f;
            overviewBlend = Mathf.MoveTowards(overviewBlend, targetBlend, Time.unscaledDeltaTime / Mathf.Max(0.01f, overviewTransitionTime));

            if (overviewBlend > 0f)
            {
                // Плавный разгон и торможение; на blend = 0 позиция совпадает с обычной — стык без рывка
                float eased = Mathf.SmoothStep(0f, 1f, overviewBlend);
                Quaternion rotation = Quaternion.Euler(currentRotationX, currentRotationY, 0f);
                Vector3 overviewPosition = overviewCenter + rotation * new Vector3(0f, 0f, -overviewDistance);
                transform.position = Vector3.Lerp(GetDesiredPosition(), overviewPosition, eased);
                velocity = Vector3.zero;
                return;
            }

            // Плавное сглаживание позиции
            transform.position = Vector3.SmoothDamp(
                transform.position,
                GetDesiredPosition(),
                ref velocity,
                smoothTime
            );
        }

        /// <summary>
        /// Позиция на орбите вокруг цели: учитывает наклон, поворот и зум
        /// </summary>
        private Vector3 GetDesiredPosition()
        {
            Quaternion rotation = Quaternion.Euler(currentRotationX, currentRotationY, 0f);
            return target.position + lookAtOffset + rotation * new Vector3(0f, 0f, -currentZoom);
        }

        /// <summary>
        /// Обновление вращения камеры
        /// </summary>
        private void UpdateCameraRotation()
        {
            // Устанавливаем вращение камеры
            Quaternion rotation = Quaternion.Euler(
                currentRotationX, 
                currentRotationY, 
                0f
            );
            
            transform.rotation = rotation;
        }

        /// <summary>
        /// Установка целевого объекта
        /// </summary>
        public void SetTarget(Transform newTarget)
        {
            Target = newTarget;
        }

        /// <summary>
        /// Установка дистанции зума
        /// </summary>
        public void SetZoom(float zoom)
        {
            currentZoom = Mathf.Clamp(zoom, minZoom, maxZoom);
        }

        /// <summary>
        /// Увеличение зума
        /// </summary>
        public void ZoomIn()
        {
            currentZoom = Mathf.Max(currentZoom - zoomSpeed * Time.deltaTime, minZoom);
        }

        /// <summary>
        /// Уменьшение зума
        /// </summary>
        public void ZoomOut()
        {
            currentZoom = Mathf.Min(currentZoom + zoomSpeed * Time.deltaTime, maxZoom);
        }

        /// <summary>
        /// Сброс камеры к дефолтным настройкам
        /// </summary>
        public void ResetCamera()
        {
            currentZoom = zoomDistance;
            currentRotationX = rotationAngle;
            currentRotationY = 45f;
        }

        /// <summary>
        /// Переключение между ортографической и перспективной проекцией
        /// </summary>
        public void ToggleProjection()
        {
            useOrthographic = !useOrthographic;
            UpdateProjection();
        }

        /// <summary>
        /// Установка ортографической проекции
        /// </summary>
        public void SetOrthographic(bool orthographic)
        {
            useOrthographic = orthographic;
            UpdateProjection();
        }

        /// <summary>
        /// Установка угла наклона камеры
        /// </summary>
        public void SetRotationAngle(float angle)
        {
            rotationAngle = Mathf.Clamp(angle, 0f, 90f);
            currentRotationX = rotationAngle;
        }

        /// <summary>
        /// Получение направления взгляда камеры в мировых координатах
        /// </summary>
        public Vector3 GetCameraForwardDirection()
        {
            return cameraComponent.transform.forward;
        }

        /// <summary>
        /// Получение позиции камеры
        /// </summary>
        public Vector3 GetCameraPosition()
        {
            return cameraComponent.transform.position;
        }

        /// <summary>
        /// Проверка, виден ли объект камерой
        /// </summary>
        public bool IsObjectVisible(GameObject obj)
        {
            if (obj == null) return false;
            
            Vector3 viewportPoint = cameraComponent.WorldToViewportPoint(obj.transform.position);
            return viewportPoint.x > 0 && viewportPoint.x < 1 && 
                   viewportPoint.y > 0 && viewportPoint.y < 1 && 
                   viewportPoint.z > 0;
        }
    }
}