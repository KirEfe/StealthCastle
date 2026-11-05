using UnityEngine;

namespace StealthCastle.Isometric3D.Mechanics
{
    /// <summary>
    /// Предмет, в который можно замаскироваться в 3D изометрической версии.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class DisguisableProp3D : MonoBehaviour
    {
        [Header("Visual Settings")]
        [SerializeField] private Renderer propRenderer; // Рендерер предмета
        [SerializeField] private Color highlightColor = new Color(1f, 0.9f, 0.6f, 1f); // Цвет подсветки
        [SerializeField] private Color selectedColor = new Color(0.8f, 1f, 0.8f, 1f); // Цвет выбранного

        [Header("Outline Settings")]
        [SerializeField] private bool useOutline = true;
        [SerializeField] private float outlineWidth = 0.02f;

        [Header("Properties")]
        [SerializeField] private string displayName = "Prop"; // Имя предмета
        [SerializeField] private bool isTreasure; // Является ли сокровищем
        [SerializeField] private bool isOccupied; // Занят ли предмет игроком

        private Color originalColor;
        private Material outlineMaterial;
        private Material[] originalMaterials;
        private bool isHighlighted;
        private bool isSelected;

        /// <summary>Имя предмета</summary>
        public string DisplayName => displayName;

        /// <summary>Назначение имени (для пропов, созданных из кода)</summary>
        public void SetDisplayName(string name) => displayName = name;

        /// <summary>Является ли сокровищем</summary>
        public bool IsTreasure => isTreasure;

        /// <summary>Занят ли предмет</summary>
        public bool IsOccupied() => isOccupied;

        /// <summary>Рендерер предмета</summary>
        public Renderer Renderer => propRenderer;

        private void Awake()
        {
            // Получаем рендерер, если не назначен
            if (propRenderer == null)
            {
                propRenderer = GetComponent<Renderer>();
            }

            // Сохраняем оригинальный цвет
            if (propRenderer != null)
            {
                originalColor = propRenderer.material.color;
                
                // Сохраняем оригинальные материалы
                originalMaterials = propRenderer.materials;
            }

            // Создаем материал outline
            if (useOutline)
            {
                outlineMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                outlineMaterial.SetFloat("_OutlineWidth", outlineWidth);
                outlineMaterial.SetColor("_OutlineColor", highlightColor);
            }
        }

        private void OnDestroy()
        {
            // Очищаем outline материал
            if (outlineMaterial != null)
            {
                Destroy(outlineMaterial);
            }
        }

        /// <summary>
        /// Подсветка предмета
        /// </summary>
        public void SetHighlighted(bool highlighted, bool selected = false)
        {
            isHighlighted = highlighted;
            isSelected = selected;

            UpdateVisual();
        }

        /// <summary>
        /// Обновление визуала предмета
        /// </summary>
        private void UpdateVisual()
        {
            if (propRenderer == null) return;

            if (useOutline && (isHighlighted || isSelected))
            {
                // Применяем outline материал
                Material[] materials = new Material[propRenderer.materials.Length];
                for (int i = 0; i < propRenderer.materials.Length; i++)
                {
                    if (i == 0) // Первый материал - outline
                    {
                        materials[i] = outlineMaterial;
                        var color = isSelected ? selectedColor : highlightColor;
                        outlineMaterial.SetColor("_OutlineColor", color);
                        // URP Lit не знает _OutlineColor — подсветка идёт основным цветом,
                        // пока нет настоящего outline-шейдера
                        outlineMaterial.color = color;
                    }
                    else
                    {
                        materials[i] = originalMaterials[i];
                    }
                }
                propRenderer.materials = materials;
            }
            else
            {
                // Возвращаем оригинальные материалы
                propRenderer.materials = originalMaterials;
            }
        }

        /// <summary>
        /// Занятие предмета
        /// </summary>
        public void SetOccupied(bool occupied)
        {
            isOccupied = occupied;
            
            // Если предмет занят, убираем подсветку
            if (occupied)
            {
                SetHighlighted(false, false);
            }
            else
            {
                // Восстанавливаем подсветку, если она была
                if (isHighlighted)
                {
                    UpdateVisual();
                }
            }
        }

        /// <summary>
        /// Получение позиции предмета
        /// </summary>
        public Vector3 GetPosition()
        {
            return transform.position;
        }

        /// <summary>
        /// Получение размера предмета
        /// </summary>
        public Vector3 GetSize()
        {
            if (propRenderer != null)
            {
                return propRenderer.bounds.size;
            }
            return Vector3.one;
        }

        private void OnDrawGizmosSelected()
        {
            // Визуализация коллайдера
            var collider = GetComponent<Collider>();
            if (collider != null)
            {
                Gizmos.color = isOccupied ? Color.red : (isSelected ? Color.green : Color.yellow);
                Gizmos.DrawWireCube(collider.bounds.center, collider.bounds.size);
            }
        }
    }
}