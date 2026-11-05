using StealthCastle.Isometric3D.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace StealthCastle.Isometric3D.Testing
{
    /// <summary>
    /// Отладочная панель для плейтестов (редактор и development-сборки). F1 — показать/скрыть.
    /// Горячие клавиши работают и при скрытой панели: F2 — режим бога, F3 — пропустить уровень,
    /// F4 — перезапустить уровень, F5 — +100 добычи, F6 — сетка проходимости уровня.
    /// </summary>
    public class DebugPanel3D : MonoBehaviour
    {
        private const int LootStep = 100;

        private bool visible;
        private string lastAction = "";
        private float lastActionTime;
        private GUIStyle boxStyle;
        private GUIStyle labelStyle;

        private GameManager3D Manager => GameManager3D.Instance;

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.f1Key.wasPressedThisFrame) visible = !visible;
            if (keyboard.f2Key.wasPressedThisFrame) ToggleGodMode();
            if (keyboard.f3Key.wasPressedThisFrame) SkipLevel();
            if (keyboard.f4Key.wasPressedThisFrame) RestartLevel();
            if (keyboard.f5Key.wasPressedThisFrame) AddLoot();
            if (keyboard.f6Key.wasPressedThisFrame) ToggleWalkability();
            DrawWalkability();
        }

        // --- Действия (публичные — их же дёргают автотесты) --------------------------------

        public void ToggleGodMode()
        {
            DebugFlags3D.GodMode = !DebugFlags3D.GodMode;
            Report(DebugFlags3D.GodMode ? "God mode ON" : "God mode OFF");
        }

        public void SkipLevel()
        {
            var manager = Manager;
            if (manager == null || manager.CurrentState != GameManager3D.GameState.Playing)
            {
                Report("Skip: only while playing a level");
                return;
            }
            manager.CompleteLevel();
            Report($"Level {manager.CurrentLevel} skipped");
        }

        public void RestartLevel()
        {
            var manager = Manager;
            bool inLevel = manager != null && FindFirstObjectByType<World.LevelGenerator3D>() != null &&
                           manager.CurrentState != GameManager3D.GameState.Hub && manager.CurrentState != GameManager3D.GameState.Loading;
            if (!inLevel)
            {
                Report("Restart: only inside a run");
                return;
            }
            manager.RestartLevel();
            Report($"Level {manager.CurrentLevel} restarted (new layout)");
        }

        public void AddLoot()
        {
            var manager = Manager;
            if (manager == null) return;
            manager.AddDebugLoot(LootStep);
            var hub = FindFirstObjectByType<Hub.HubController3D>();
            if (hub != null) hub.RefreshStatus();
            Report(manager.Run != null ? $"+{LootStep} loot carried" : $"+{LootStep} loot to the stash");
        }

        // --- Сетка проходимости ------------------------------------------------------------

        private World.WalkabilityCheck3D.Report walkability;
        private Mesh walkabilityMesh;
        private int walkabilityLevel;

        public World.WalkabilityCheck3D.Report Walkability => walkability;

        public void ToggleWalkability()
        {
            if (walkability != null)
            {
                walkability = null;
                Report("Walkability overlay OFF");
                return;
            }
            RebuildWalkability();
        }

        private void RebuildWalkability()
        {
            var player = FindFirstObjectByType<Player.PlayerController3D>();
            var pieces = new System.Collections.Generic.List<World.LevelPiece3D>();
            foreach (var piece in FindObjectsByType<World.LevelPiece3D>(FindObjectsSortMode.None))
                if (piece.gameObject.activeInHierarchy) pieces.Add(piece);
            if (player == null || pieces.Count == 0)
            {
                Report("Walkability: no level here");
                return;
            }
            var exits = new System.Collections.Generic.List<Vector3>();
            foreach (var exit in FindObjectsByType<World.LevelExit3D>(FindObjectsSortMode.None))
                exits.Add(exit.transform.position);

            walkability = World.WalkabilityCheck3D.AnalyzeLevel(player.transform.position, pieces, exits,
                World.WalkabilityGrid3D.Settings.Default);
            walkabilityLevel = Manager != null ? Manager.CurrentLevel : 0;
            if (walkability.grid == null)
            {
                Report("Walkability: " + string.Join("; ", walkability.issues));
                walkability = null;
                return;
            }
            walkabilityMesh = walkability.grid.BuildOverlayMesh(walkability.Paths, walkabilityMesh);
            Report("Walkability: " + WalkabilitySummary());
        }

        private string WalkabilitySummary()
        {
            if (walkability.links.Count == 0) return "no exits found";
            var parts = new System.Collections.Generic.List<string>();
            foreach (var link in walkability.links)
            {
                string status = link.status switch
                {
                    World.WalkabilityCheck3D.Status.Ok => "OK",
                    World.WalkabilityCheck3D.Status.Narrow => "NARROW",
                    World.WalkabilityCheck3D.Status.NoPath => "NO PATH",
                    _ => "BLOCKED",
                };
                parts.Add($"{link.from}->{link.to} {status}");
            }
            return string.Join(", ", parts);
        }

        private void DrawWalkability()
        {
            if (walkability == null || walkabilityMesh == null) return;
            // Новый уровень — сетка устарела
            if (Manager != null && Manager.CurrentLevel != walkabilityLevel)
            {
                RebuildWalkability();
                if (walkability == null) return;
            }
            var material = World.WalkabilityGrid3D.OverlayMaterial;
            material.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.LessEqual);
            Graphics.DrawMesh(walkabilityMesh, Matrix4x4.identity, material, 0);
        }

        private void OnDestroy()
        {
            if (walkabilityMesh != null) Destroy(walkabilityMesh);
        }

        private void Report(string text)
        {
            lastAction = text;
            lastActionTime = Time.unscaledTime;
            Debug.Log($"[DebugPanel3D] {text}");
        }

        // --- Отрисовка -------------------------------------------------------------------

        private void OnGUI()
        {
            // Масштаб под высоту экрана (вёрстка в пикселях при 720p)
            float scale = Screen.height / 720f;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            EnsureStyles();

            bool flash = Time.unscaledTime - lastActionTime < 2.5f && lastAction != "";
            if (!visible)
            {
                string hint = DebugFlags3D.GodMode ? "F1: debug   |   GOD MODE" : "F1: debug";
                GUI.Label(new Rect(12f, 690f, 400f, 24f), flash ? $"{hint}   |   {lastAction}" : hint, labelStyle);
                return;
            }

            var manager = Manager;
            GUILayout.BeginArea(new Rect(12f, 110f, 250f, 290f), boxStyle);
            GUILayout.Label("<b>DEBUG</b>  (F1 to hide)", labelStyle);
            if (manager != null)
            {
                string run = manager.Run != null ? $"loot {manager.Run.CarriedLoot}" : "no run";
                GUILayout.Label($"State: {manager.CurrentState}\nLevel: {manager.CurrentLevel}   ({run})\n" +
                                $"Stash: {Meta.MetaProgress3D.Current.loot}", labelStyle);
            }
            GUILayout.Space(6f);

            bool god = GUILayout.Toggle(DebugFlags3D.GodMode, "  God mode  [F2]");
            if (god != DebugFlags3D.GodMode) ToggleGodMode();
            if (GUILayout.Button("Skip level  [F3]")) SkipLevel();
            if (GUILayout.Button("Restart level  [F4]")) RestartLevel();
            if (GUILayout.Button($"+{LootStep} loot  [F5]")) AddLoot();
            bool grid = GUILayout.Toggle(walkability != null, "  Walkability grid  [F6]");
            if (grid != (walkability != null)) ToggleWalkability();
            if (walkability != null) GUILayout.Label(WalkabilitySummary(), labelStyle);

            GUILayout.Space(6f);
            if (flash) GUILayout.Label($"<color=#FFD24A>{lastAction}</color>", labelStyle);
            GUILayout.EndArea();
        }

        private void EnsureStyles()
        {
            if (boxStyle != null) return;
            var background = new Texture2D(1, 1);
            background.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.8f));
            background.Apply();
            boxStyle = new GUIStyle(GUI.skin.box) { padding = new RectOffset(10, 10, 8, 8) };
            boxStyle.normal.background = background;
            labelStyle = new GUIStyle(GUI.skin.label) { richText = true, fontSize = 13 };
            labelStyle.normal.textColor = Color.white;
        }
    }
}
