using System;
using System.Collections.Generic;
using UnityEngine;

namespace StealthCastle.Isometric3D.World
{
    /// <summary>
    /// Сетка проходимости в плане: в каждой клетке проверяется, встанет ли туда капсула игрока.
    /// Ширина прохода учитывается сама: где капсула не помещается, свободных клеток нет.
    /// Вторая проверка — капсулой радиусом minPassWidth/2: клетки, где помещается только «впритык»,
    /// считаются узкими. Путь ищется поиском в ширину по свободным клеткам.
    /// Используется валидатором кусков, генератором и лабораторией баланса.
    /// </summary>
    public class WalkabilityGrid3D
    {
        public enum Cell : byte { NoFloor, Blocked, Narrow, Free }

        [Serializable]
        public struct Settings
        {
            public float cellSize;
            public float agentRadius;
            public float agentHeight;
            public float minPassWidth; // Проход уже этого — узкое место
            public LayerMask obstacleMask;

            public static Settings Default => new Settings
            {
                cellSize = 0.25f,
                agentRadius = 0.4f,
                agentHeight = 1.8f,
                minPassWidth = 1.2f,
                obstacleMask = DefaultObstacleMask(),
            };
        }

        /// <summary>Всё, кроме игрока, стражей и служебных слоёв. Триггеры не учитываются</summary>
        public static LayerMask DefaultObstacleMask()
        {
            int mask = ~0;
            foreach (var name in new[] { "Player", "DisguisedPlayer", "Enemy", "Ignore Raycast", "UI" })
            {
                int layer = LayerMask.NameToLayer(name);
                if (layer >= 0) mask &= ~(1 << layer);
            }
            return mask;
        }

        public readonly int Width;
        public readonly int Height;
        public readonly float CellSize;
        public readonly Vector3 Origin; // Угол клетки (0,0), y — уровень пола
        public readonly Settings Config;
        private readonly Cell[] cells;

        private static readonly Collider[] OverlapBuffer = new Collider[32];

        private WalkabilityGrid3D(Vector3 origin, int width, int height, Settings settings)
        {
            Origin = origin;
            Width = width;
            Height = height;
            CellSize = settings.cellSize;
            Config = settings;
            cells = new Cell[width * height];
        }

        public Cell this[int x, int z] => cells[z * Width + x];
        public Cell this[Vector2Int c] => cells[c.y * Width + c.x];
        public bool InBounds(Vector2Int c) => c.x >= 0 && c.y >= 0 && c.x < Width && c.y < Height;
        public static bool IsWalkable(Cell cell) => cell == Cell.Free || cell == Cell.Narrow;

        public Vector3 CellCenter(Vector2Int c) =>
            Origin + new Vector3((c.x + 0.5f) * CellSize, 0f, (c.y + 0.5f) * CellSize);

        public Vector2Int CellOf(Vector3 world) => new Vector2Int(
            Mathf.FloorToInt((world.x - Origin.x) / CellSize),
            Mathf.FloorToInt((world.z - Origin.z) / CellSize));

        /// <summary>
        /// Строит сетку над прямоугольником area (по x, z) с полом на area.min.y.
        /// ignore — коллайдеры, которые не считаются препятствием (например, заглушки открытых проёмов).
        /// </summary>
        public static WalkabilityGrid3D Build(PhysicsScene physics, Bounds area, Settings settings,
            Func<Collider, bool> ignore = null)
        {
            int width = Mathf.Max(1, Mathf.RoundToInt(area.size.x / settings.cellSize));
            int height = Mathf.Max(1, Mathf.RoundToInt(area.size.z / settings.cellSize));
            var grid = new WalkabilityGrid3D(new Vector3(area.min.x, area.min.y, area.min.z), width, height, settings);

            for (int z = 0; z < height; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    var center = grid.CellCenter(new Vector2Int(x, z));
                    Cell cell;
                    if (!HasFloor(physics, center, settings))
                        cell = Cell.NoFloor;
                    else if (Overlaps(physics, center, settings.agentRadius, settings, ignore))
                        cell = Cell.Blocked;
                    else if (Overlaps(physics, center, settings.minPassWidth * 0.5f, settings, ignore))
                        cell = Cell.Narrow;
                    else
                        cell = Cell.Free;
                    grid.cells[z * width + x] = cell;
                }
            }
            return grid;
        }

        private static bool HasFloor(PhysicsScene physics, Vector3 center, Settings settings)
        {
            // Луч сверху вниз: стартует внутри стены — стену не видит и находит пол под ней.
            // Клетка над стеной всё равно станет занятой по проверке капсулой
            var from = center + Vector3.up * 2.5f;
            return physics.Raycast(from, Vector3.down, out _, 3f, settings.obstacleMask, QueryTriggerInteraction.Ignore);
        }

        private static bool Overlaps(PhysicsScene physics, Vector3 floorPoint, float radius, Settings settings,
            Func<Collider, bool> ignore)
        {
            var bottom = floorPoint + Vector3.up * (radius + 0.05f);
            var top = floorPoint + Vector3.up * Mathf.Max(radius + 0.05f, settings.agentHeight - radius);
            int count = physics.OverlapCapsule(bottom, top, radius, OverlapBuffer, settings.obstacleMask,
                QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                if (ignore == null || !ignore(OverlapBuffer[i])) return true;
            }
            return false;
        }

        /// <summary>Ближайшая проходимая клетка в радиусе maxDistance от точки (или null)</summary>
        public Vector2Int? NearestWalkable(Vector3 world, float maxDistance, Func<Vector2Int, bool> filter = null)
        {
            var origin = CellOf(world);
            int range = Mathf.CeilToInt(maxDistance / CellSize);
            Vector2Int? best = null;
            float bestSqr = float.MaxValue;
            for (int dz = -range; dz <= range; dz++)
            {
                for (int dx = -range; dx <= range; dx++)
                {
                    var c = new Vector2Int(origin.x + dx, origin.y + dz);
                    if (!InBounds(c) || !IsWalkable(this[c])) continue;
                    if (filter != null && !filter(c)) continue;
                    float sqr = (CellCenter(c) - world).sqrMagnitude;
                    if (sqr < bestSqr && sqr <= maxDistance * maxDistance)
                    {
                        bestSqr = sqr;
                        best = c;
                    }
                }
            }
            return best;
        }

        /// <summary>Все клетки, достижимые из start (индекс z * Width + x)</summary>
        public bool[] Flood(Vector2Int start)
        {
            var reached = new bool[cells.Length];
            if (!InBounds(start) || !IsWalkable(this[start])) return reached;
            var queue = new Queue<Vector2Int>();
            reached[start.y * Width + start.x] = true;
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                var c = queue.Dequeue();
                foreach (var d in Neighbours)
                {
                    var n = c + d;
                    if (!InBounds(n) || !IsWalkable(this[n])) continue;
                    if (d.x != 0 && d.y != 0 &&
                        (!IsWalkable(this[c.x + d.x, c.y]) || !IsWalkable(this[c.x, c.y + d.y]))) continue;
                    int index = n.y * Width + n.x;
                    if (reached[index]) continue;
                    reached[index] = true;
                    queue.Enqueue(n);
                }
            }
            return reached;
        }

        /// <summary>Есть ли достижимая клетка, для центра которой within(центр) истинно, в радиусе radius от точки</summary>
        public bool AnyReached(bool[] reached, Vector3 around, float radius, Func<Vector3, bool> within)
        {
            var min = CellOf(around - new Vector3(radius, 0f, radius));
            var max = CellOf(around + new Vector3(radius, 0f, radius));
            for (int z = Mathf.Max(0, min.y); z <= Mathf.Min(Height - 1, max.y); z++)
            {
                for (int x = Mathf.Max(0, min.x); x <= Mathf.Min(Width - 1, max.x); x++)
                {
                    if (reached[z * Width + x] && within(CellCenter(new Vector2Int(x, z)))) return true;
                }
            }
            return false;
        }

        private static readonly Vector2Int[] Neighbours =
        {
            new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1),
            new Vector2Int(1, 1), new Vector2Int(1, -1), new Vector2Int(-1, 1), new Vector2Int(-1, -1),
        };

        /// <summary>
        /// Кратчайший путь (по числу шагов, 8 соседей, без срезания углов) между клетками.
        /// wideOnly — только по клеткам Free, то есть не уже minPassWidth. null — пути нет.
        /// </summary>
        public List<Vector2Int> FindPath(Vector2Int from, Vector2Int to, bool wideOnly = false)
        {
            bool Passable(Vector2Int c) => InBounds(c) && (wideOnly ? this[c] == Cell.Free : IsWalkable(this[c]));
            if (!Passable(from) || !Passable(to)) return null;

            var previous = new int[cells.Length];
            for (int i = 0; i < previous.Length; i++) previous[i] = -1;
            int start = from.y * Width + from.x;
            int goal = to.y * Width + to.x;
            previous[start] = start;

            var queue = new Queue<Vector2Int>();
            queue.Enqueue(from);
            while (queue.Count > 0)
            {
                var c = queue.Dequeue();
                if (c == to) break;
                foreach (var d in Neighbours)
                {
                    var n = c + d;
                    if (!Passable(n)) continue;
                    // Диагональ только если обе боковые клетки свободны — не протискиваемся в угол
                    if (d.x != 0 && d.y != 0 &&
                        (!Passable(new Vector2Int(c.x + d.x, c.y)) || !Passable(new Vector2Int(c.x, c.y + d.y)))) continue;
                    int index = n.y * Width + n.x;
                    if (previous[index] != -1) continue;
                    previous[index] = c.y * Width + c.x;
                    queue.Enqueue(n);
                }
            }
            if (previous[goal] == -1) return null;

            var path = new List<Vector2Int>();
            for (int i = goal; ; i = previous[i])
            {
                path.Add(new Vector2Int(i % Width, i / Width));
                if (i == start) break;
            }
            path.Reverse();
            return path;
        }

        // --- Оверлей ---------------------------------------------------------------------

        public static readonly Color FreeColor = new Color(0.2f, 0.85f, 0.3f, 0.28f);
        public static readonly Color NarrowColor = new Color(0.55f, 0.85f, 0.25f, 0.18f);
        public static readonly Color BlockedColor = new Color(0.95f, 0.15f, 0.1f, 0.35f);
        public static readonly Color PathColor = new Color(0.3f, 0.7f, 1f, 0.85f);
        public static readonly Color ChokeColor = new Color(1f, 0.85f, 0.1f, 0.95f);

        /// <summary>
        /// Меш с цветами вершин: свободно — зелёный, впритык — бледно-зелёный, занято — красный.
        /// Клетки путей — голубые, узкие места на пути — жёлтые
        /// </summary>
        public Mesh BuildOverlayMesh(IEnumerable<List<Vector2Int>> paths = null, Mesh reuse = null)
        {
            var overrides = new Dictionary<int, Color>();
            if (paths != null)
            {
                foreach (var path in paths)
                {
                    if (path == null) continue;
                    foreach (var c in path)
                        overrides[c.y * Width + c.x] = this[c] == Cell.Narrow ? ChokeColor : PathColor;
                }
            }

            var vertices = new List<Vector3>();
            var colors = new List<Color>();
            var triangles = new List<int>();
            float inset = CellSize * 0.06f;
            const float lift = 0.04f;
            for (int z = 0; z < Height; z++)
            {
                for (int x = 0; x < Width; x++)
                {
                    int index = z * Width + x;
                    Color color;
                    if (!overrides.TryGetValue(index, out color))
                    {
                        switch (cells[index])
                        {
                            case Cell.Free: color = FreeColor; break;
                            case Cell.Narrow: color = NarrowColor; break;
                            case Cell.Blocked: color = BlockedColor; break;
                            default: continue;
                        }
                    }
                    var min = Origin + new Vector3(x * CellSize + inset, lift, z * CellSize + inset);
                    float size = CellSize - inset * 2f;
                    int v = vertices.Count;
                    vertices.Add(min);
                    vertices.Add(min + new Vector3(0f, 0f, size));
                    vertices.Add(min + new Vector3(size, 0f, size));
                    vertices.Add(min + new Vector3(size, 0f, 0f));
                    for (int i = 0; i < 4; i++) colors.Add(color);
                    triangles.Add(v); triangles.Add(v + 1); triangles.Add(v + 2);
                    triangles.Add(v); triangles.Add(v + 2); triangles.Add(v + 3);
                }
            }

            var mesh = reuse != null ? reuse : new Mesh { name = "WalkabilityOverlay", hideFlags = HideFlags.HideAndDontSave };
            mesh.Clear();
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Material overlayMaterial;

        /// <summary>Прозрачный материал с цветами вершин для оверлея (редактор и игра)</summary>
        public static Material OverlayMaterial
        {
            get
            {
                if (overlayMaterial != null) return overlayMaterial;
                var shader = Shader.Find("Hidden/Internal-Colored") ?? Shader.Find("Sprites/Default");
                overlayMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                overlayMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                overlayMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                overlayMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
                overlayMaterial.SetInt("_ZWrite", 0);
                return overlayMaterial;
            }
        }
    }
}
