using System.Collections.Generic;
using System.Text;
using StealthCastle.Isometric3D.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace StealthCastle.Isometric3D.EditorTools
{
    /// <summary>
    /// Пакетная проверка генератора: N раскладок по разным сидам собираются в изолированной сцене
    /// и проверяются физически (путь от спавна до выхода). Критерий этапа 0 — ноль провалов.
    /// Берёт генератор из открытой сцены (Isometric3D_Game).
    /// </summary>
    public static class LayoutBatchCheck3D
    {
        [MenuItem("StealthCastle/3D/Check Level Layouts (500)")]
        private static void RunFromMenu()
        {
            var report = Run(500, 1);
            Debug.Log(report);
            EditorUtility.DisplayDialog("Check Level Layouts", report.Length > 1500 ? report.Substring(0, 1500) + "\n…(полный отчёт в консоли)" : report, "OK");
        }

        public static string Run(int count, int firstSeed)
        {
            var generator = Object.FindFirstObjectByType<LevelGenerator3D>();
            if (generator == null) return "Нет LevelGenerator3D в открытой сцене — открой Isometric3D_Game.";

            var scene = EditorSceneManager.NewPreviewScene();
            var watch = System.Diagnostics.Stopwatch.StartNew();
            int ok = 0, narrow = 0, failed = 0, fallback = 0;
            long blockedCells = 0, floorCells = 0; // Санитарная проверка: физика сцены видит стены
            var lengths = new SortedDictionary<int, int>();
            var usage = new SortedDictionary<string, int>();
            var failures = new List<string>();
            try
            {
                for (int i = 0; i < count; i++)
                {
                    int seed = firstSeed + i;
                    var layout = generator.PlanLayoutInfo(new System.Random(seed));
                    if (layout == null) return "Генератор не смог собрать раскладку (нет старта или выхода).";
                    if (layout.isFallback) fallback++;
                    lengths[layout.pieces.Count] = lengths.TryGetValue(layout.pieces.Count, out var n) ? n + 1 : 1;

                    var spawned = new List<GameObject>();
                    var pieces = new List<LevelPiece3D>();
                    var exits = new List<Vector3>();
                    Vector3? start = null;
                    for (int p = 0; p < layout.pieces.Count; p++)
                    {
                        var data = layout.pieces[p];
                        usage[data.name] = usage.TryGetValue(data.name, out var u) ? u + 1 : 1;
                        var go = (GameObject)PrefabUtility.InstantiatePrefab(data.Prefab, scene);
                        go.transform.position = layout.positions[p];
                        LevelGenerator3D.ApplyDoorways(go, layout.openDirs[p]);
                        spawned.Add(go);
                        pieces.Add(go.GetComponent<LevelPiece3D>());
                        foreach (var exit in go.GetComponentsInChildren<LevelExit3D>()) exits.Add(exit.transform.position);
                        if (p == 0)
                        {
                            var spawn = go.GetComponentInChildren<SpawnPoint3D>();
                            start = spawn != null ? spawn.transform.position : go.transform.position;
                        }
                    }

                    var report = WalkabilityCheck3D.AnalyzeLevel(start ?? Vector3.zero, pieces, exits, WalkabilityGrid3D.Settings.Default);
                    if (report.grid != null)
                    {
                        for (int z = 0; z < report.grid.Height; z++)
                        for (int x = 0; x < report.grid.Width; x++)
                        {
                            var cell = report.grid[x, z];
                            if (cell == WalkabilityGrid3D.Cell.NoFloor) continue;
                            floorCells++;
                            if (cell == WalkabilityGrid3D.Cell.Blocked) blockedCells++;
                        }
                    }
                    if (report.HasErrors)
                    {
                        failed++;
                        if (failures.Count < 10)
                        {
                            var names = layout.pieces.ConvertAll(d => d.name);
                            failures.Add($"seed {seed}: {string.Join(" → ", names)} | {string.Join("; ", report.issues)} " +
                                         string.Join(" ", report.links.ConvertAll(l => $"{l.from}->{l.to}:{l.status}")));
                        }
                    }
                    else if (report.HasNarrow) narrow++;
                    else ok++;

                    foreach (var go in spawned) Object.DestroyImmediate(go);
                }
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }

            var sb = new StringBuilder();
            sb.AppendLine($"Layouts: {count} (seeds {firstSeed}..{firstSeed + count - 1}), {watch.ElapsedMilliseconds} ms");
            sb.AppendLine($"OK {ok}, narrow-only {narrow}, FAILED {failed}, fallback (start+exit only) {fallback}");
            sb.AppendLine($"Blocked cells: {(floorCells > 0 ? 100f * blockedCells / floorCells : 0f):0}% of floor (0% = physics saw nothing)");
            sb.Append("Length:");
            foreach (var kv in lengths) sb.Append($" {kv.Key}×{kv.Value}");
            sb.AppendLine();
            sb.Append("Pieces:");
            foreach (var kv in usage) sb.Append($" {kv.Key.Split('_')[0]}×{kv.Value}");
            sb.AppendLine();
            foreach (var f in failures) sb.AppendLine(f);
            return sb.ToString();
        }
    }
}
