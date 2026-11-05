using System.Collections.Generic;
using UnityEngine;

namespace StealthCastle.Isometric3D.World
{
    /// <summary>
    /// Стена любого размера: коллайдер (режет движение и лучи зрения) + кладка точно под размер.
    /// Пивот — центр основания. Меши кладки генерирует редактор (WallBlocks3D) и хранит в общей библиотеке;
    /// визуалы — дочерние Visual_Base и Visual_Upper. Высокая стена разделена на цоколь и верх:
    /// верх «срезается», когда стена закрывает игрока от камеры (WallCutaway3D).
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class WallBlock3D : MonoBehaviour
    {
        public enum Style
        {
            Stone, // Кладка: материалы [раствор, камень, тёмный камень]
            Wood,  // Доски (столы, койки, стеллажи): материалы [основа, доска]
        }

        public const float RowHeight = 0.33f;       // Высота ряда кладки
        public const float CutawayMinHeight = 1.4f; // Ниже — не срезается
        public const int BaseRows = 2;              // Цоколь, который остаётся при срезе

        [SerializeField] private Vector3 size = new Vector3(2f, 2.6f, 0.5f);
        [SerializeField] private Style style = Style.Stone;
        [SerializeField] private Material[] materials;
        [SerializeField] private Transform baseVisual;
        [SerializeField] private Transform upperVisual;

        private static readonly List<WallBlock3D> active = new List<WallBlock3D>();

        private float cutaway = 1f;      // 1 — стена целиком, 0 — один цоколь
        private float cutawayTarget = 1f;

        public static IReadOnlyList<WallBlock3D> Active => active;
        public Vector3 Size => size;
        public Style WallStyle => style;
        public Material[] Materials => materials;
        public bool CanCutaway => upperVisual != null;

        /// <summary>Пятно стены на полу (мировые XZ)</summary>
        public Bounds Footprint => GetComponent<BoxCollider>().bounds;

        /// <summary>Задать размер и стиль (визуалы пересобирает редактор)</summary>
        public void Configure(Vector3 wallSize, Style wallStyle, Material[] wallMaterials)
        {
            size = wallSize;
            style = wallStyle;
            materials = wallMaterials;
        }

        /// <summary>Подключить собранные визуалы (редактор)</summary>
        public void SetVisuals(Transform baseTransform, Transform upperTransform)
        {
            baseVisual = baseTransform;
            upperVisual = upperTransform;
        }

        /// <summary>Срезать верх стены (плавно)</summary>
        public void SetCutaway(bool lowered) => cutawayTarget = lowered ? 0f : 1f;

        private void OnEnable()
        {
            if (upperVisual == null) return;
            active.Add(this);
            WallCutaway3D.Ensure();
        }

        private void OnDisable()
        {
            active.Remove(this);
        }

        private void Update()
        {
            if (upperVisual == null || Mathf.Approximately(cutaway, cutawayTarget)) return;
            cutaway = Mathf.MoveTowards(cutaway, cutawayTarget, Time.unscaledDeltaTime * 5f);
            upperVisual.localScale = new Vector3(1f, Mathf.Max(cutaway, 0.001f), 1f);
            var upperRenderer = upperVisual.GetComponent<Renderer>();
            if (upperRenderer != null) upperRenderer.enabled = cutaway > 0.01f;
        }

        /// <summary>Высота цоколя для стены этой высоты; 0 — стена не делится</summary>
        public static float SplitHeight(float height) => height >= CutawayMinHeight ? BaseRows * RowHeightFor(height) : 0f;

        // --- Генерация ----------------------------------------------------------------------

        private static float RowHeightFor(float height) => height / Mathf.Max(1, Mathf.RoundToInt(height / RowHeight));

        /// <summary>Ключ меша в библиотеке: одинаковые стены делят меши</summary>
        public static string MeshKey(Vector3 size, Style style) => $"{style}_{size.x:0.00}_{size.y:0.00}_{size.z:0.00}";

        /// <summary>[цоколь, верх или null] для стены размера size</summary>
        public static Mesh[] GenerateMeshes(Vector3 size, Style style)
        {
            string key = MeshKey(size, style);
            var rnd = new System.Random(key.GetHashCode());
            float splitY = SplitHeight(size.y);
            bool split = splitY > 0f;
            var lower = new MeshBuilder(style == Style.Stone ? 3 : 2);
            var upper = split ? new MeshBuilder(style == Style.Stone ? 3 : 2) : null;

            if (style == Style.Stone) BuildStone(size, split ? splitY : size.y, lower, upper, rnd);
            else BuildWood(size, split ? splitY : size.y, lower, upper, rnd);

            return new[] { lower.ToMesh(key + "_base"), upper?.ToMesh(key + "_upper") };
        }

        /// <summary>Каменная кладка: раствор-сердцевина, камни рядами со смещением на всех гранях, плиты-парапет сверху</summary>
        private static void BuildStone(Vector3 size, float splitY, MeshBuilder lower, MeshBuilder upper, System.Random rnd)
        {
            const float cap = 0.1f, out_ = 0.03f, depth = 0.08f, gap = 0.025f;
            float hx = size.x * 0.5f, hz = size.z * 0.5f;
            float body = size.y - cap;
            int rows = Mathf.Max(1, Mathf.RoundToInt(body / RowHeight));
            float rowH = body / rows;

            // Сердцевина-раствор (тёмная, видна в швах)
            AddSplitBox(lower, upper, splitY, new Vector3(-hx + 0.02f, 0f, -hz + 0.02f), new Vector3(hx - 0.02f, body, hz - 0.02f), 0);

            for (int row = 0; row < rows; row++)
            {
                float y0 = row * rowH + gap * 0.5f, y1 = (row + 1) * rowH - gap * 0.5f;
                float stagger = row % 2 == 0 ? 0f : 0.27f;
                // Грани вдоль X (±Z) перекрывают углы, грани вдоль Z (±X) — между ними
                foreach (int side in new[] { -1, 1 })
                {
                    Course(rnd, -hx - out_, hx + out_, stagger, (a, b, mat, j) =>
                    {
                        float z0 = side > 0 ? hz - depth + out_ : -hz - out_ - j;
                        float z1 = side > 0 ? hz + out_ + j : -hz + depth - out_;
                        AddSplitBox(lower, upper, splitY, new Vector3(a, y0, z0), new Vector3(b, y1, z1), mat);
                    });
                    Course(rnd, -hz + depth - out_, hz - depth + out_, stagger + 0.13f, (a, b, mat, j) =>
                    {
                        float x0 = side > 0 ? hx - depth + out_ : -hx - out_ - j;
                        float x1 = side > 0 ? hx + out_ + j : -hx + depth - out_;
                        AddSplitBox(lower, upper, splitY, new Vector3(x0, y0, a), new Vector3(x1, y1, b), mat);
                    });
                }
            }

            // Парапет: плиты вдоль длинной стороны
            bool alongX = size.x >= size.z;
            float length = alongX ? size.x : size.z;
            int slabs = Mathf.Max(1, Mathf.RoundToInt(length / 0.8f));
            float slab = (length + out_ * 2f) / slabs;
            for (int i = 0; i < slabs; i++)
            {
                float a = -length * 0.5f - out_ + i * slab + 0.01f, b = a + slab - 0.02f;
                var min = alongX ? new Vector3(a, body, -hz - out_ - 0.02f) : new Vector3(-hx - out_ - 0.02f, body, a);
                var max = alongX ? new Vector3(b, size.y, hz + out_ + 0.02f) : new Vector3(hx + out_ + 0.02f, size.y, b);
                AddSplitBox(lower, upper, splitY, min, max, rnd.NextDouble() > 0.5 ? 2 : 1);
            }
        }

        /// <summary>Дощатый блок: основа + горизонтальные доски по граням + настил сверху</summary>
        private static void BuildWood(Vector3 size, float splitY, MeshBuilder lower, MeshBuilder upper, System.Random rnd)
        {
            const float top = 0.06f, out_ = 0.02f, gap = 0.02f;
            float hx = size.x * 0.5f, hz = size.z * 0.5f;
            float body = size.y - top;
            AddSplitBox(lower, upper, splitY, new Vector3(-hx, 0f, -hz), new Vector3(hx, body, hz), 0);

            int rows = Mathf.Max(1, Mathf.RoundToInt(body / 0.2f));
            float rowH = body / rows;
            for (int row = 0; row < rows; row++)
            {
                float y0 = row * rowH + gap * 0.5f, y1 = (row + 1) * rowH - gap * 0.5f;
                foreach (int side in new[] { -1, 1 })
                {
                    float z = side * (hz + out_ * 0.5f);
                    AddSplitBox(lower, upper, splitY, new Vector3(-hx - out_, y0, z - out_ * 0.5f), new Vector3(hx + out_, y1, z + out_ * 0.5f), 1);
                    float x = side * (hx + out_ * 0.5f);
                    AddSplitBox(lower, upper, splitY, new Vector3(x - out_ * 0.5f, y0, -hz), new Vector3(x + out_ * 0.5f, y1, hz), 1);
                }
            }

            // Настил: доски поперёк длинной стороны
            bool alongX = size.x >= size.z;
            float length = alongX ? size.x : size.z;
            int boards = Mathf.Max(1, Mathf.RoundToInt(length / 0.25f));
            float board = (length + out_ * 2f) / boards;
            for (int i = 0; i < boards; i++)
            {
                float a = -length * 0.5f - out_ + i * board + 0.008f, b = a + board - 0.016f;
                float lift = (float)rnd.NextDouble() * 0.01f;
                var min = alongX ? new Vector3(a, body, -hz - out_) : new Vector3(-hx - out_, body, a);
                var max = alongX ? new Vector3(b, size.y + lift, hz + out_) : new Vector3(hx + out_, size.y + lift, b);
                AddSplitBox(lower, upper, splitY, min, max, 1);
            }
        }

        /// <summary>Ряд камней от from до to: случайная ширина, материал и выступ; края ряда обрезаются</summary>
        private static void Course(System.Random rnd, float from, float to, float stagger, System.Action<float, float, int, float> stone)
        {
            const float gap = 0.025f;
            float x = from - stagger;
            while (x < to)
            {
                float width = 0.38f + (float)rnd.NextDouble() * 0.28f;
                float a = Mathf.Max(x, from), b = Mathf.Min(x + width, to);
                if (b - a > 0.06f)
                {
                    int mat = rnd.NextDouble() > 0.28 ? 1 : 2;
                    float jitter = (float)rnd.NextDouble() * 0.015f;
                    stone(a + gap * 0.5f, b - gap * 0.5f, mat, jitter);
                }
                x += width;
            }
        }

        /// <summary>Бокс в цоколь и/или верх: часть выше splitY уходит в верх (в его координатах — от splitY)</summary>
        private static void AddSplitBox(MeshBuilder lower, MeshBuilder upper, float splitY, Vector3 min, Vector3 max, int material)
        {
            if (upper == null || max.y <= splitY + 0.001f)
            {
                lower.Box(min, max, material, Vector3.zero);
                return;
            }
            if (min.y >= splitY - 0.001f)
            {
                upper.Box(min - Vector3.up * splitY, max - Vector3.up * splitY, material, Vector3.up * splitY);
                return;
            }
            lower.Box(min, new Vector3(max.x, splitY, max.z), material, Vector3.zero);
            upper.Box(new Vector3(min.x, 0f, min.z), max - Vector3.up * splitY, material, Vector3.up * splitY);
        }

        /// <summary>Меш из боксов с плоскими нормалями, подмешами по материалам и кубической развёрткой (1 текстура на 2 м)</summary>
        private sealed class MeshBuilder
        {
            private readonly List<Vector3> vertices = new List<Vector3>();
            private readonly List<Vector3> normals = new List<Vector3>();
            private readonly List<Vector2> uvs = new List<Vector2>();
            private readonly List<int>[] triangles;

            public MeshBuilder(int submeshes)
            {
                triangles = new List<int>[submeshes];
                for (int i = 0; i < submeshes; i++) triangles[i] = new List<int>();
            }

            /// <param name="uvOffset">Сдвиг для развёртки: верх стены продолжает текстуру цоколя</param>
            public void Box(Vector3 min, Vector3 max, int material, Vector3 uvOffset)
            {
                var c = new[]
                {
                    new Vector3(min.x, min.y, min.z), new Vector3(max.x, min.y, min.z), new Vector3(max.x, max.y, min.z), new Vector3(min.x, max.y, min.z),
                    new Vector3(min.x, min.y, max.z), new Vector3(max.x, min.y, max.z), new Vector3(max.x, max.y, max.z), new Vector3(min.x, max.y, max.z),
                };
                Quad(c[0], c[3], c[2], c[1], Vector3.back, material, uvOffset);
                Quad(c[5], c[6], c[7], c[4], Vector3.forward, material, uvOffset);
                Quad(c[4], c[7], c[3], c[0], Vector3.left, material, uvOffset);
                Quad(c[1], c[2], c[6], c[5], Vector3.right, material, uvOffset);
                Quad(c[3], c[7], c[6], c[2], Vector3.up, material, uvOffset); // Низ не нужен: всё стоит на полу или на соседе
            }

            private void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal, int material, Vector3 uvOffset)
            {
                int start = vertices.Count;
                foreach (var v in new[] { a, b, c, d })
                {
                    vertices.Add(v);
                    normals.Add(normal);
                    var p = (v + uvOffset) * 0.5f;
                    uvs.Add(normal.x != 0f ? new Vector2(p.z, p.y) : normal.y != 0f ? new Vector2(p.x, p.z) : new Vector2(p.x, p.y));
                }
                var tris = triangles[material];
                tris.Add(start); tris.Add(start + 1); tris.Add(start + 2);
                tris.Add(start); tris.Add(start + 2); tris.Add(start + 3);
            }

            public Mesh ToMesh(string meshName)
            {
                var mesh = new Mesh { name = meshName };
                mesh.indexFormat = vertices.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
                mesh.SetVertices(vertices);
                mesh.SetNormals(normals);
                mesh.SetUVs(0, uvs);
                mesh.subMeshCount = triangles.Length;
                for (int i = 0; i < triangles.Length; i++) mesh.SetTriangles(triangles[i], i);
                mesh.RecalculateBounds();
                mesh.RecalculateTangents();
                return mesh;
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.8f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(new Vector3(0f, size.y * 0.5f, 0f), size);
        }
#endif
    }
}
