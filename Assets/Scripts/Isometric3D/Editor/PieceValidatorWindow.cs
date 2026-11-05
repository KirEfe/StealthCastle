using System.Collections.Generic;
using StealthCastle.Isometric3D.World;
using UnityEditor;
using UnityEngine;

namespace StealthCastle.Isometric3D.EditorTools
{
    /// <summary>
    /// Проверка всех кусков уровня разом: авторинг (заглушки, PieceData, стражи) и проходимость
    /// (проёмы, спавн, выход, сундуки, пропы, ширина прохода, фартук у проёмов).
    /// Куски грузятся в изолированную сцену префаба — открытая сцена не трогается.
    /// </summary>
    public class PieceValidatorWindow : EditorWindow
    {
        public class Result
        {
            public string path;
            public string name;
            public readonly List<string> errors = new List<string>();
            public readonly List<string> warnings = new List<string>();
            public bool expanded;
        }

        private List<Result> results;
        private Vector2 scroll;

        [MenuItem("StealthCastle/3D/Piece Validator")]
        public static void Open() => GetWindow<PieceValidatorWindow>("Piece Validator");

        /// <summary>Все префабы с LevelPiece3D в проекте</summary>
        public static List<Result> ValidateAll()
        {
            var list = new List<Result>();
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (asset == null || asset.GetComponent<LevelPiece3D>() == null) continue;
                list.Add(Validate(path));
            }
            list.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            return list;
        }

        public static Result Validate(string prefabPath)
        {
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var piece = root.GetComponent<LevelPiece3D>();
                var result = new Result { path = prefabPath, name = root.name };
                result.errors.AddRange(PieceAuthoring3D.Validate(piece));

                var report = WalkabilityCheck3D.AnalyzePiece(piece, WalkabilityGrid3D.Settings.Default);
                result.errors.AddRange(report.issues);
                result.warnings.AddRange(report.warnings);
                foreach (var link in report.links)
                {
                    if (link.status == WalkabilityCheck3D.Status.Ok) continue;
                    var message = $"{link.from} → {link.to}: {WalkabilityCheck3D.Describe(link.status)}";
                    if (link.status == WalkabilityCheck3D.Status.Narrow) result.warnings.Add(message);
                    else result.errors.Add(message);
                }
                return result;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>Текстовый отчёт — для консоли и автотестов</summary>
        public static string Summary(List<Result> list)
        {
            var sb = new System.Text.StringBuilder();
            int errors = list.FindAll(r => r.errors.Count > 0).Count;
            int warnings = list.FindAll(r => r.errors.Count == 0 && r.warnings.Count > 0).Count;
            sb.AppendLine($"{list.Count} pieces: {list.Count - errors - warnings} OK, {warnings} with warnings, {errors} with errors");
            foreach (var r in list)
            {
                foreach (var e in r.errors) sb.AppendLine($"{r.name} ERROR {e}");
                foreach (var w in r.warnings) sb.AppendLine($"{r.name} WARN {w}");
            }
            return sb.ToString();
        }

        private void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("Проверить все куски", EditorStyles.toolbarButton, GUILayout.Width(150f)))
                    results = ValidateAll();
                GUILayout.FlexibleSpace();
            }

            if (results == null)
            {
                EditorGUILayout.HelpBox(
                    "Проверяет каждый кусок: авторинг, путь между проёмами, спавн и выход, сундуки, пропы для превращения, " +
                    $"проходы уже {WalkabilityGrid3D.Settings.Default.minPassWidth:0.0} м и пустой «фартук» " +
                    $"{WalkabilityCheck3D.ApronDepth:0.0} м у проёмов. Сетку куска показывает его инспектор.",
                    MessageType.Info);
                return;
            }

            int withErrors = results.FindAll(r => r.errors.Count > 0).Count;
            int withWarnings = results.FindAll(r => r.errors.Count == 0 && r.warnings.Count > 0).Count;
            EditorGUILayout.LabelField(
                $"Кусков: {results.Count}   OK: {results.Count - withErrors - withWarnings}   " +
                $"с предупреждениями: {withWarnings}   с ошибками: {withErrors}", EditorStyles.boldLabel);

            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (var result in results)
            {
                string icon = result.errors.Count > 0 ? "console.erroricon.sml"
                    : result.warnings.Count > 0 ? "console.warnicon.sml" : "TestPassed";
                using (new EditorGUILayout.HorizontalScope())
                {
                    var content = EditorGUIUtility.IconContent(icon);
                    content.text = $" {result.name}";
                    bool hasMessages = result.errors.Count + result.warnings.Count > 0;
                    if (hasMessages)
                        result.expanded = EditorGUILayout.Foldout(result.expanded, content, true);
                    else
                        EditorGUILayout.LabelField(content);
                    if (GUILayout.Button("Открыть", GUILayout.Width(70f)))
                        AssetDatabase.OpenAsset(AssetDatabase.LoadAssetAtPath<GameObject>(result.path));
                }
                if (!result.expanded) continue;
                EditorGUI.indentLevel++;
                foreach (var error in result.errors) EditorGUILayout.HelpBox(error, MessageType.Error);
                foreach (var warning in result.warnings) EditorGUILayout.HelpBox(warning, MessageType.Warning);
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndScrollView();
        }
    }
}
