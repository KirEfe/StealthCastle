using UnityEngine;

namespace StealthCastle.Isometric3D.Player
{
    /// <summary>
    /// Визуальная система маскировки игрока для 3D изометрической версии.
    /// Управляет сменой модели игрока при маскировке под предметы.
    /// </summary>
    [RequireComponent(typeof(PlayerController3D))]
    public class PlayerDisguiseVisual3D : MonoBehaviour
    {
        [Header("Player Model")]
        [SerializeField] private GameObject defaultPlayerModel; // Дефолтная модель игрока
        [SerializeField] private Transform modelContainer; // Контейнер для моделей

        [Header("Disguise Settings")]
        [SerializeField] private bool scaleToPropSize = true; // Масштабировать под размер пропа
        [SerializeField] private float disguiseFadeSpeed = 5f; // Скорость смены визуала

        private GameObject currentPlayerModel;
        private GameObject currentDisguiseModel;
        private PlayerController3D playerController;
        private Mechanics.DisguisableProp3D currentProp;
        private float disguiseVerticalOffset; // Сдвиг модели пропа вниз, к ногам игрока

        /// <summary>Текущая модель маскировки</summary>
        public GameObject CurrentDisguiseModel => currentDisguiseModel;

        /// <summary>Текущий проп маскировки</summary>
        public Mechanics.DisguisableProp3D CurrentProp => currentProp;

        private void Awake()
        {
            playerController = GetComponent<PlayerController3D>();

            // Создаем контейнер для моделей, если не существует
            if (modelContainer == null)
            {
                modelContainer = new GameObject("ModelContainer").transform;
                modelContainer.SetParent(transform, false);
                modelContainer.localPosition = Vector3.zero;
                modelContainer.localRotation = Quaternion.identity;
                modelContainer.localScale = Vector3.one;
            }

            // Устанавливаем дефолтную модель игрока
            if (defaultPlayerModel != null)
            {
                currentPlayerModel = Instantiate(defaultPlayerModel, modelContainer);
                currentPlayerModel.transform.localPosition = Vector3.zero;
                currentPlayerModel.transform.localRotation = Quaternion.identity;
                currentPlayerModel.transform.localScale = Vector3.one;
                SetLayerRecursively(currentPlayerModel, gameObject.layer);
            }
        }

        /// <summary>
        /// Модели — на слое игрока: рендерер рисует его отдельным проходом и силуэтом сквозь стены
        /// </summary>
        private static void SetLayerRecursively(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform)
            {
                SetLayerRecursively(child.gameObject, layer);
            }
        }

        private void OnDestroy()
        {
            // Очищаем текущие модели
            ClearDisguise();
            ClearPlayerModel();
        }

        /// <summary>
        /// Применение маскировки
        /// </summary>
        public void ApplyDisguise(Mechanics.DisguisableProp3D prop)
        {
            if (prop == null) return;

            // Удаляем текущую маскировку
            ClearDisguise();

            // Сохраняем ссылку на проп
            currentProp = prop;

            // Создаем модель маскировки
            CreateDisguiseModel(prop);

            // Скрываем дефолтную модель игрока
            if (currentPlayerModel != null)
            {
                currentPlayerModel.SetActive(false);
            }
        }

        /// <summary>
        /// Создание модели маскировки
        /// </summary>
        private void CreateDisguiseModel(Mechanics.DisguisableProp3D prop)
        {
            if (prop.Renderer == null) return;

            // Получаем mesh и материалы из пропа
            var propRenderer = prop.Renderer;
            MeshFilter meshFilter = propRenderer.GetComponent<MeshFilter>();
            
            if (meshFilter != null)
            {
                // Создаем новый объект для маскировки
                currentDisguiseModel = new GameObject("DisguiseModel") { layer = gameObject.layer };
                currentDisguiseModel.transform.SetParent(modelContainer, false);

                // Добавляем компоненты
                var disguiseFilter = currentDisguiseModel.AddComponent<MeshFilter>();
                var disguiseRenderer = currentDisguiseModel.AddComponent<MeshRenderer>();

                // Копируем mesh и материалы (shared — без лишних копий ассетов)
                disguiseFilter.sharedMesh = meshFilter.sharedMesh;
                disguiseRenderer.sharedMaterials = propRenderer.sharedMaterials;

                // Меш скопирован в своих локальных единицах — повторяем мировой масштаб пропа
                if (scaleToPropSize)
                {
                    Vector3 propScale = propRenderer.transform.lossyScale;
                    Vector3 containerScale = modelContainer.lossyScale;
                    currentDisguiseModel.transform.localScale = new Vector3(
                        propScale.x / containerScale.x,
                        propScale.y / containerScale.y,
                        propScale.z / containerScale.z
                    );
                }
                else
                {
                    currentDisguiseModel.transform.localScale = Vector3.one;
                }

                // Проп стоит на полу под ногами игрока, а не висит на высоте его центра
                // Низ меша — на пол: пивот у примитивов в центре, у моделей из Blender — в основании
                var playerCollider = GetComponent<Collider>();
                float meshBottom = meshFilter.sharedMesh.bounds.min.y * propRenderer.transform.lossyScale.y;
                disguiseVerticalOffset = playerCollider != null
                    ? playerCollider.bounds.min.y - meshBottom - modelContainer.position.y
                    : 0f;

                currentDisguiseModel.transform.localPosition = Vector3.up * disguiseVerticalOffset;
                currentDisguiseModel.transform.localRotation = Quaternion.identity;
            }
            else
            {
                Debug.LogWarning("[PlayerDisguiseVisual3D] Prop has no MeshFilter component!");
            }
        }

        /// <summary>
        /// Очистка маскировки
        /// </summary>
        public void ClearDisguise()
        {
            if (currentDisguiseModel != null)
            {
                Destroy(currentDisguiseModel);
                currentDisguiseModel = null;
            }

            currentProp = null;

            // Показываем дефолтную модель игрока
            if (currentPlayerModel != null)
            {
                currentPlayerModel.SetActive(true);
            }
        }

        /// <summary>
        /// Очистка модели игрока
        /// </summary>
        private void ClearPlayerModel()
        {
            if (currentPlayerModel != null)
            {
                Destroy(currentPlayerModel);
                currentPlayerModel = null;
            }
        }

        /// <summary>
        /// Установка дефолтной модели игрока
        /// </summary>
        public void SetDefaultPlayerModel(GameObject model)
        {
            ClearPlayerModel();
            ClearDisguise();

            if (model != null)
            {
                defaultPlayerModel = model;
                currentPlayerModel = Instantiate(defaultPlayerModel, modelContainer);
                currentPlayerModel.transform.localPosition = Vector3.zero;
                currentPlayerModel.transform.localRotation = Quaternion.identity;
                currentPlayerModel.transform.localScale = Vector3.one;
                SetLayerRecursively(currentPlayerModel, gameObject.layer);
            }
        }

        /// <summary>
        /// Установка видимости игрока
        /// </summary>
        public void SetPlayerVisible(bool visible)
        {
            if (currentPlayerModel != null)
            {
                currentPlayerModel.SetActive(visible && currentDisguiseModel == null);
            }
            
            if (currentDisguiseModel != null)
            {
                currentDisguiseModel.SetActive(visible);
            }
        }

        /// <summary>
        /// Получение размеров текущей модели
        /// </summary>
        public Vector3 GetCurrentModelSize()
        {
            if (currentDisguiseModel != null)
            {
                var renderer = currentDisguiseModel.GetComponent<Renderer>();
                if (renderer != null)
                {
                    return renderer.bounds.size;
                }
            }
            else if (currentPlayerModel != null)
            {
                var renderer = currentPlayerModel.GetComponent<Renderer>();
                if (renderer != null)
                {
                    return renderer.bounds.size;
                }
            }
            
            return Vector3.one;
        }

        /// <summary>
        /// Обновление позиции и вращения модели
        /// </summary>
        private void Update()
        {
            // Обновляем позицию и вращение модели маскировки
            if (currentDisguiseModel != null && playerController != null)
            {
                // Модель маскировки следует за визуальным transform игрока
                if (playerController.VisualTransform != null)
                {
                    currentDisguiseModel.transform.position = playerController.VisualTransform.position + Vector3.up * disguiseVerticalOffset;
                    currentDisguiseModel.transform.rotation = playerController.VisualTransform.rotation;
                }
            }
        }
    }
}