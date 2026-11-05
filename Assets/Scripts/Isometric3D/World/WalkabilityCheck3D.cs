using System.Collections.Generic;
using UnityEngine;

namespace StealthCastle.Isometric3D.World
{
    /// <summary>
    /// Проверки проходимости поверх WalkabilityGrid3D: отдельный кусок (проёмы, спавн, выход) и собранный уровень.
    /// </summary>
    public static class WalkabilityCheck3D
    {
        public enum Status { Ok, Narrow, NoPath, Unreachable }

        public class Link
        {
            public string from;
            public string to;
            public Status status;
            public List<Vector2Int> path; // Путь по любым проходимым клеткам (null — пути нет)
        }

        public class Report
        {
            public WalkabilityGrid3D grid;
            public readonly List<Link> links = new List<Link>();
            public readonly List<string> issues = new List<string>();   // Ошибки: кусок неиграбелен или ломает стыки
            public readonly List<string> warnings = new List<string>(); // Играбельно, но стоит поправить
            public bool HasErrors => issues.Count > 0 ||
                                     links.Exists(l => l.status == Status.NoPath || l.status == Status.Unreachable);
            public bool HasNarrow => links.Exists(l => l.status == Status.Narrow);

            public IEnumerable<List<Vector2Int>> Paths
            {
                get { foreach (var link in links) if (link.path != null) yield return link.path; }
            }
        }

        private const float DoorInset = 0.6f; // Точка проёма — чуть внутри куска
        private const float DoorSearchDepth = 1.4f; // Насколько вглубь ищем свободную клетку у проёма
        private const float TargetSearchRadius = 1.5f; // Подход к спавну и выходу
        public const float ApronDepth = 1.5f; // «Фартук» у проёма: зона, где не должно быть предметов
        public const float PropReach = 2f; // Радиус выбора предмета для превращения (DisguiseSystem3D.scanRadius)
        private const float EyeHeight = 0.9f; // Откуда меряется дистанция до предмета — центр капсулы

        /// <summary>Кусок отдельно: заглушки его проёмов не считаются препятствием (в уровне они открыты)</summary>
        public static Report AnalyzePiece(LevelPiece3D piece, WalkabilityGrid3D.Settings settings)
        {
            var root = piece.transform;
            var doorBlockers = new HashSet<Transform>();
            foreach (var dir in piece.Doors)
            {
                var blocker = root.Find("Blocker_" + dir);
                if (blocker != null) doorBlockers.Add(blocker);
            }
            bool IgnoreCollider(Collider c)
            {
                for (var t = c.transform; t != null && t != root; t = t.parent)
                    if (doorBlockers.Contains(t)) return true;
                return false;
            }

            Physics.SyncTransforms();
            float half = LevelPiece3D.Size * 0.5f;
            var area = new Bounds();
            area.SetMinMax(root.position - new Vector3(half, 0f, half), root.position + new Vector3(half, 0f, half));
            var grid = WalkabilityGrid3D.Build(piece.gameObject.scene.GetPhysicsScene(), area, settings, IgnoreCollider);

            var report = new Report { grid = grid };
            var points = new List<(string name, Vector2Int? cell)>();
            foreach (var dir in piece.Doors)
                points.Add(($"проём {dir}", DoorCell(grid, root, dir)));
            foreach (var spawn in piece.GetComponentsInChildren<SpawnPoint3D>(true))
                points.Add(("спавн", grid.NearestWalkable(spawn.transform.position, TargetSearchRadius)));
            foreach (var exit in piece.GetComponentsInChildren<LevelExit3D>(true))
                points.Add(("выход", grid.NearestWalkable(exit.transform.position, TargetSearchRadius)));

            Connect(report, points);
            CheckReach(piece, report, points.Count > 0 ? points[0].cell : null);
            CheckAprons(piece, report, IgnoreCollider);
            return report;
        }

        /// <summary>Сундуки открываются, к пропам для превращения можно подойти — из той же области, что и первый проём</summary>
        private static void CheckReach(LevelPiece3D piece, Report report, Vector2Int? from)
        {
            if (from == null) return;
            var grid = report.grid;
            var reached = grid.Flood(from.Value);

            foreach (var chest in piece.GetComponentsInChildren<LootChest3D>(true))
            {
                var center = chest.transform.position;
                float radius = chest.PickupRadius;
                bool ok = grid.AnyReached(reached, center, radius, c =>
                    new Vector2(c.x - center.x, c.z - center.z).sqrMagnitude <= radius * radius);
                if (!ok) report.issues.Add($"сундук {chest.name}: не подойти ближе {radius:0.#} м — не открыть");
            }

            foreach (var prop in piece.GetComponentsInChildren<Mechanics.DisguisableProp3D>(true))
            {
                var collider = prop.GetComponent<Collider>();
                bool ok = grid.AnyReached(reached, prop.transform.position, PropReach + 1f, c =>
                {
                    var eye = c + Vector3.up * EyeHeight;
                    var target = collider != null ? collider.ClosestPoint(eye) : prop.transform.position;
                    return (target - eye).sqrMagnitude <= PropReach * PropReach;
                });
                if (!ok) report.warnings.Add($"проп {prop.name}: не подойти на {PropReach:0.#} м — в него не превратиться");
            }
        }

        /// <summary>У каждого проёма зона на всю ширину и ApronDepth вглубь свободна: иначе на стыке предметы двух кусков сужают проход</summary>
        private static void CheckAprons(LevelPiece3D piece, Report report, System.Func<Collider, bool> ignore)
        {
            var root = piece.transform;
            var physics = piece.gameObject.scene.GetPhysicsScene();
            var buffer = new Collider[32];
            foreach (var dir in piece.Doors)
            {
                var door = LevelPiece3D.DoorLocalPosition(dir);
                var inward = root.TransformDirection(-ConnectionPoint3D.DirectionToVector(dir));
                var edge = root.TransformPoint(new Vector3(door.x, 0f, door.y));
                var center = edge + inward * (ApronDepth * 0.5f) + Vector3.up * 1f;
                var halfExtents = new Vector3(LevelPiece3D.DoorWidth * 0.5f - 0.05f, 0.9f, ApronDepth * 0.5f);
                int count = physics.OverlapBox(center, halfExtents, buffer, Quaternion.LookRotation(inward),
                    WalkabilityGrid3D.DefaultObstacleMask(), QueryTriggerInteraction.Ignore);

                var names = new SortedSet<string>();
                for (int i = 0; i < count; i++)
                {
                    var collider = buffer[i];
                    if (ignore(collider)) continue;
                    var part = TopLevelPart(root, collider.transform);
                    if (part == null || IsShellPart(part.name)) continue;
                    names.Add(part.name);
                }
                if (names.Count > 0)
                    report.issues.Add($"фартук проёма {dir} ({ApronDepth:0.#} м) занят: {string.Join(", ", names)}");
            }
        }

        private static Transform TopLevelPart(Transform root, Transform t)
        {
            while (t != null && t.parent != root) t = t.parent;
            return t;
        }

        /// <summary>Пол, стены периметра и заглушки — их строит «Перестроить пол и стены»</summary>
        public static bool IsShellPart(string name) =>
            name == "Floor" || name.StartsWith("Blocker_") ||
            name.StartsWith("Wall_North") || name.StartsWith("Wall_East") ||
            name.StartsWith("Wall_South") || name.StartsWith("Wall_West");

        /// <summary>Собранный уровень: от игрока до каждого выхода</summary>
        public static Report AnalyzeLevel(Vector3 start, IReadOnlyList<LevelPiece3D> pieces,
            IReadOnlyList<Vector3> exits, WalkabilityGrid3D.Settings settings)
        {
            var report = new Report();
            if (pieces.Count == 0)
            {
                report.issues.Add("No level pieces");
                return report;
            }

            float half = LevelPiece3D.Size * 0.5f;
            var min = pieces[0].transform.position;
            var max = min;
            foreach (var piece in pieces)
            {
                min = Vector3.Min(min, piece.transform.position);
                max = Vector3.Max(max, piece.transform.position);
            }
            var area = new Bounds();
            area.SetMinMax(new Vector3(min.x - half, min.y, min.z - half), new Vector3(max.x + half, min.y, max.z + half));

            Physics.SyncTransforms();
            var grid = WalkabilityGrid3D.Build(pieces[0].gameObject.scene.GetPhysicsScene(), area, settings);
            report.grid = grid;

            var points = new List<(string name, Vector2Int? cell)> { ("start", grid.NearestWalkable(start, TargetSearchRadius)) };
            for (int i = 0; i < exits.Count; i++)
                points.Add((exits.Count > 1 ? $"exit {i + 1}" : "exit", grid.NearestWalkable(exits[i], TargetSearchRadius)));
            Connect(report, points);
            return report;
        }

        /// <summary>Первая точка соединяется со всеми остальными (связность транзитивна)</summary>
        private static void Connect(Report report, List<(string name, Vector2Int? cell)> points)
        {
            foreach (var point in points)
            {
                if (point.cell == null)
                    report.issues.Add($"{point.name}: нет свободного места рядом — завален");
            }
            if (points.Count < 2) return;

            var first = points[0];
            for (int i = 1; i < points.Count; i++)
            {
                var other = points[i];
                var link = new Link { from = first.name, to = other.name };
                if (first.cell == null || other.cell == null)
                {
                    link.status = Status.Unreachable;
                }
                else
                {
                    link.path = report.grid.FindPath(first.cell.Value, other.cell.Value);
                    if (link.path == null)
                        link.status = Status.NoPath;
                    else
                        link.status = report.grid.FindPath(first.cell.Value, other.cell.Value, wideOnly: true) != null
                            ? Status.Ok
                            : Status.Narrow;
                }
                report.links.Add(link);
            }
        }

        private static Vector2Int? DoorCell(WalkabilityGrid3D grid, Transform root, ConnectionPoint3D.Dir dir)
        {
            var door = LevelPiece3D.DoorLocalPosition(dir);
            var inward = -ConnectionPoint3D.DirectionToVector(dir);
            var edge = root.TransformPoint(new Vector3(door.x, 0f, door.y));
            var target = edge + root.TransformDirection(inward) * DoorInset;
            var lateral = root.TransformDirection(new Vector3(inward.z, 0f, -inward.x));
            var depthAxis = root.TransformDirection(inward);

            // Свободная клетка в прямоугольнике проёма: ширина проёма × DoorSearchDepth вглубь
            bool InDoorway(Vector2Int c)
            {
                var offset = grid.CellCenter(c) - edge;
                float depth = Vector3.Dot(offset, depthAxis);
                return depth >= 0f && depth <= DoorSearchDepth &&
                       Mathf.Abs(Vector3.Dot(offset, lateral)) <= LevelPiece3D.DoorWidth * 0.5f;
            }
            return grid.NearestWalkable(target, LevelPiece3D.DoorWidth, InDoorway);
        }

        public static string Describe(Status status) => status switch
        {
            Status.Ok => "OK",
            Status.Narrow => "только через узкое место",
            Status.NoPath => "НЕТ ПУТИ",
            _ => "точка завалена",
        };
    }
}
