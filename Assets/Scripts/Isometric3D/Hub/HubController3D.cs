using System.Collections.Generic;
using System.Text;
using StealthCastle.Isometric3D.Core;
using StealthCastle.Isometric3D.Meta;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace StealthCastle.Isometric3D.Hub
{
    /// <summary>
    /// Логово воров между забегами. Игрок ходит по комнате; у станции — подсказка, E открывает её окно:
    /// стол с картой (в замок), верстак (артефакт на забег), скупщик (улучшения за добычу), полка трофеев (статистика).
    /// После забега первым делом показывается его итог.
    /// </summary>
    public class HubController3D : MonoBehaviour
    {
        [Header("Base player stats (preview of the loadout)")]
        [SerializeField] private float baseBreath = 6f;   // DisguiseSystem3D.disguiseDuration игрока в игровой сцене
        [SerializeField] private float baseScanRadius = 2f;
        [SerializeField] private float baseWalkSpeed = 5f;

        [Header("Fallback")]
        [SerializeField] private string gameScene = "Isometric3D_Game"; // Если в сцене нет GameManager3D

        private const string DefaultFooter = "W/S - choose     E - confirm     Esc - back";
        private const float MessageTime = 2.5f;

        private Player.PlayerController3D player;
        private HubUI3D ui;
        private HubStation3D[] stations;
        private HubStation3D openStation;
        private RunReport3D openReport;
        private int cursor;
        private float messageTimer;

        private MetaProgress3D Progress => MetaProgress3D.Current;
        private MetaConfig3D Config => MetaConfig3D.Load();
        private bool WindowOpen => openStation != null || openReport != null;

        private void Start()
        {
            player = FindFirstObjectByType<Player.PlayerController3D>();
            stations = FindObjectsByType<HubStation3D>(FindObjectsSortMode.None);
            ui = GetComponent<HubUI3D>();
            if (ui == null) ui = gameObject.AddComponent<HubUI3D>();
            ui.Build();
            RefreshStatus();

            if (Config == null)
            {
                Debug.LogWarning("[HubController3D] MetaConfig3D not found in Resources — run StealthCastle → 3D → Build Hub Scene");
            }

            // Итог только что закончившегося забега
            if (RunReport3D.Pending != null)
            {
                openReport = RunReport3D.Pending;
                RunReport3D.Pending = null;
                OpenWindow();
            }
        }

        private void Update()
        {
            if (messageTimer > 0f)
            {
                messageTimer -= Time.unscaledDeltaTime;
                if (messageTimer <= 0f) ui.SetMessage(null);
            }

            if (WindowOpen)
            {
                HandleWindowInput();
                return;
            }

            var gameManager = GameManager3D.Instance;
            if (gameManager != null && gameManager.CurrentState != GameManager3D.GameState.Hub)
            {
                ui.SetPrompt(null); // Уже уходим в замок
                return;
            }

            var nearby = FindNearbyStation();
            ui.SetPrompt(nearby != null ? $"[E]  {nearby.Prompt}" : null);
            if (nearby != null && ConfirmPressed())
            {
                openStation = nearby;
                cursor = InitialCursor(nearby);
                OpenWindow();
            }
        }

        private HubStation3D FindNearbyStation()
        {
            if (player == null) return null;
            HubStation3D best = null;
            float bestDistance = float.MaxValue;
            foreach (var station in stations)
            {
                if (station == null) continue;
                float distance = station.FlatSqrDistance(player.transform.position);
                if (distance <= station.Radius * station.Radius && distance < bestDistance)
                {
                    best = station;
                    bestDistance = distance;
                }
            }
            return best;
        }

        // --- Окно станции -------------------------------------------------------------

        private void OpenWindow()
        {
            player?.SetInputEnabled(false);
            ui.SetPrompt(null);
            Redraw();
        }

        private void CloseWindow()
        {
            openStation = null;
            openReport = null;
            ui.HideWindow();
            player?.SetInputEnabled(true);
            RefreshStatus();
        }

        private void HandleWindowInput()
        {
            int count = OptionCount();
            if (count > 0 && UpPressed()) { cursor = (cursor + count - 1) % count; Redraw(); }
            if (count > 0 && DownPressed()) { cursor = (cursor + 1) % count; Redraw(); }

            if (BackPressed()) CloseWindow();
            else if (ConfirmPressed()) Confirm();
        }

        private int OptionCount()
        {
            if (openReport != null || openStation == null || Config == null) return 0;
            return openStation.StationKind switch
            {
                HubStation3D.Kind.StartRun => 2,
                HubStation3D.Kind.Artifacts => Config.Artifacts.Count + 1, // + «без артефакта»
                HubStation3D.Kind.Upgrades => Config.Upgrades.Count,
                _ => 0,
            };
        }

        private int InitialCursor(HubStation3D station)
        {
            if (station.StationKind != HubStation3D.Kind.Artifacts || Config == null) return 0;
            var selected = Config.FindArtifact(Progress.selectedArtifact);
            for (int i = 0; i < Config.Artifacts.Count; i++)
            {
                if (Config.Artifacts[i] == selected) return i + 1;
            }
            return 0;
        }

        private void Redraw()
        {
            if (openReport != null)
            {
                ui.ShowWindow(openReport.Escaped ? "Back home safe" : "Caught!", null, -1, ReportText(openReport), "E - continue");
                return;
            }

            switch (openStation.StationKind)
            {
                case HubStation3D.Kind.StartRun:
                    ui.ShowWindow(openStation.Title, new[] { "Enter the castle", "Not yet" }, cursor, LoadoutText(), DefaultFooter);
                    break;
                case HubStation3D.Kind.Artifacts:
                    ui.ShowWindow(openStation.Title, ArtifactLines(), cursor, ArtifactDetails(), DefaultFooter);
                    break;
                case HubStation3D.Kind.Upgrades:
                    ui.ShowWindow(openStation.Title, UpgradeLines(), cursor, UpgradeDetails(), DefaultFooter);
                    break;
                default:
                    ui.ShowWindow(openStation.Title, null, -1, RecordsText(), "E / Esc - close");
                    break;
            }
        }

        private void Confirm()
        {
            if (openReport != null || openStation == null || Config == null)
            {
                CloseWindow();
                return;
            }

            switch (openStation.StationKind)
            {
                case HubStation3D.Kind.StartRun:
                    if (cursor == 0) StartRun();
                    else CloseWindow();
                    return;
                case HubStation3D.Kind.Artifacts:
                    ConfirmArtifact(cursor == 0 ? null : Config.Artifacts[cursor - 1]);
                    break;
                case HubStation3D.Kind.Upgrades:
                    ConfirmUpgrade(Config.Upgrades[cursor]);
                    break;
                default:
                    CloseWindow();
                    return;
            }
            RefreshStatus();
            Redraw();
        }

        private void StartRun()
        {
            ui.HideWindow();
            openStation = null;
            var gameManager = GameManager3D.Instance;
            if (gameManager != null) gameManager.StartRunFromHub();
            else SceneManager.LoadScene(gameScene);
        }

        private void ConfirmArtifact(ArtifactData3D artifact)
        {
            if (artifact == null)
            {
                Progress.selectedArtifact = "";
                ShowMessage("Going in light: no artifact");
            }
            else if (Progress.IsUnlocked(artifact))
            {
                Progress.selectedArtifact = artifact.name;
                ShowMessage($"{artifact.DisplayName} equipped");
            }
            else if (Progress.loot >= artifact.UnlockCost)
            {
                Progress.loot -= artifact.UnlockCost;
                Progress.unlockedArtifacts.Add(artifact.name);
                Progress.selectedArtifact = artifact.name;
                ShowMessage($"{artifact.DisplayName} unlocked and equipped!");
            }
            else
            {
                ShowMessage($"Not enough loot: need {artifact.UnlockCost}");
                return;
            }
            MetaProgress3D.Save();
        }

        private void ConfirmUpgrade(MetaConfig3D.Upgrade upgrade)
        {
            int level = Progress.GetUpgradeLevel(upgrade.id);
            if (level >= upgrade.MaxLevel)
            {
                ShowMessage($"{upgrade.displayName} is maxed out");
                return;
            }
            int cost = upgrade.costs[level];
            if (Progress.loot < cost)
            {
                ShowMessage($"Not enough loot: need {cost}");
                return;
            }
            Progress.loot -= cost;
            Progress.SetUpgradeLevel(upgrade.id, level + 1);
            MetaProgress3D.Save();
            ShowMessage($"{upgrade.displayName} {level + 1}/{upgrade.MaxLevel} bought");
        }

        private void ShowMessage(string text)
        {
            ui.SetMessage(text);
            messageTimer = MessageTime;
        }

        public void RefreshStatus()
        {
            var artifact = Config != null ? Config.FindArtifact(Progress.selectedArtifact) : null;
            ui.SetStatus(Progress.loot, artifact != null ? artifact.DisplayName : null);
        }

        // --- Тексты -------------------------------------------------------------------

        private List<string> ArtifactLines()
        {
            var lines = new List<string> { Progress.selectedArtifact == "" ? "No artifact  <color=#8FD18F>(equipped)</color>" : "No artifact" };
            foreach (var artifact in Config.Artifacts)
            {
                string state = artifact.name == Progress.selectedArtifact ? "  <color=#8FD18F>(equipped)</color>"
                    : Progress.IsUnlocked(artifact) ? ""
                    : $"  <color=#9A948C>[{artifact.UnlockCost} loot]</color>";
                lines.Add(artifact.DisplayName + state);
            }
            return lines;
        }

        private string ArtifactDetails()
        {
            if (cursor == 0) return "Travel light. No bonuses, nothing to lose.";
            var artifact = Config.Artifacts[cursor - 1];
            var text = new StringBuilder();
            text.AppendLine($"<b>{artifact.DisplayName}</b>");
            text.AppendLine(artifact.Description);
            text.AppendLine();
            text.Append(EffectsText(artifact));
            if (!Progress.IsUnlocked(artifact))
            {
                text.AppendLine();
                text.AppendLine($"Unlock: {artifact.UnlockCost} loot (you have {Progress.loot})");
            }
            return text.ToString();
        }

        private static string EffectsText(ArtifactData3D artifact)
        {
            var text = new StringBuilder();
            if (artifact.DisguiseDurationBonus != 0f) text.AppendLine($"Breath {artifact.DisguiseDurationBonus:+0.#;-0.#} s");
            if (artifact.ScanRadiusBonus != 0f) text.AppendLine($"Scan radius {artifact.ScanRadiusBonus:+0.#;-0.#} m");
            if (artifact.MovementSpeedBonus != 0f) text.AppendLine($"Walk speed {artifact.MovementSpeedBonus:+0.#;-0.#} m/s");
            if (artifact.GuardVisionBonus != 0f) text.AppendLine($"Guard sight {artifact.GuardVisionBonus * 100f:+0;-0}%");
            if (!Mathf.Approximately(artifact.WeightMultiplier, 1f)) text.AppendLine($"{artifact.AffectedTag} rooms x{artifact.WeightMultiplier:0.#}");
            return text.ToString();
        }

        private List<string> UpgradeLines()
        {
            var lines = new List<string>();
            foreach (var upgrade in Config.Upgrades)
            {
                int level = Progress.GetUpgradeLevel(upgrade.id);
                string price = level >= upgrade.MaxLevel ? "<color=#8FD18F>MAX</color>" : $"<color=#9A948C>{upgrade.costs[level]} loot</color>";
                lines.Add($"{upgrade.displayName}  {level}/{upgrade.MaxLevel}   {price}");
            }
            return lines;
        }

        private string UpgradeDetails()
        {
            if (Config.Upgrades.Count == 0) return "";
            var upgrade = Config.Upgrades[cursor];
            int level = Progress.GetUpgradeLevel(upgrade.id);
            return $"<b>{upgrade.displayName}</b>\n{upgrade.description}\n\nLevel {level}/{upgrade.MaxLevel}\n\n{LoadoutText()}";
        }

        /// <summary>Итоговое снаряжение на забег: базовые параметры + улучшения + артефакт</summary>
        private string LoadoutText()
        {
            var artifact = Config != null ? Config.FindArtifact(Progress.selectedArtifact) : null;
            var stats = Loadout3D.Compute(baseBreath, baseScanRadius, baseWalkSpeed, artifact, Progress, Config);
            var text = new StringBuilder();
            text.AppendLine("<b>Your loadout</b>");
            text.AppendLine($"Artifact: {(artifact != null ? artifact.DisplayName : "none")}");
            text.AppendLine($"Breath in disguise: {stats.Breath:0.#} s");
            text.AppendLine($"Scan radius: {stats.ScanRadius:0.#} m");
            text.AppendLine($"Walk speed: {stats.WalkSpeed:0.#} m/s");
            text.AppendLine($"Loot kept if caught: {stats.KeptWhenCaught * 100f:0}%");
            if (artifact != null)
            {
                if (artifact.GuardVisionBonus != 0f) text.AppendLine($"Guard sight: {artifact.GuardVisionBonus * 100f:+0;-0}%");
                if (!Mathf.Approximately(artifact.WeightMultiplier, 1f)) text.AppendLine($"{artifact.AffectedTag} rooms x{artifact.WeightMultiplier:0.#}");
            }
            return text.ToString();
        }

        private string RecordsText()
        {
            var p = Progress;
            return $"Heists: {p.runs}   (came back on your own: {p.escapes})\n" +
                   $"Deepest level: {p.deepestLevel}\n" +
                   $"Levels cleared: {p.levelsCleared}\n" +
                   $"Chests opened: {p.chestsOpened}\n" +
                   $"Loot brought home: {p.totalLootBanked}\n\n" +
                   $"Stash now: {p.loot}";
        }

        private string ReportText(RunReport3D report)
        {
            if (report.Escaped)
            {
                return $"You slipped out after level {report.LevelsCleared}.\n\n" +
                       $"Loot brought home: {report.Kept}\n" +
                       $"Chests opened: {report.Chests}\n\n" +
                       $"Stash: {Progress.loot}";
            }
            return $"The guards caught you on level {report.Level}.\n" +
                   $"Levels cleared: {report.LevelsCleared}\n\n" +
                   $"Loot carried: {report.Carried}\n" +
                   $"Saved in your pockets: {report.Kept}\n\n" +
                   $"Stash: {Progress.loot}";
        }

        // --- Ввод ---------------------------------------------------------------------

        private static bool ConfirmPressed()
        {
            var k = Keyboard.current;
            var g = Gamepad.current;
            return (k != null && (k.eKey.wasPressedThisFrame || k.enterKey.wasPressedThisFrame || k.spaceKey.wasPressedThisFrame))
                || (g != null && g.buttonSouth.wasPressedThisFrame);
        }

        private static bool BackPressed()
        {
            var k = Keyboard.current;
            var g = Gamepad.current;
            return (k != null && (k.escapeKey.wasPressedThisFrame || k.qKey.wasPressedThisFrame || k.backspaceKey.wasPressedThisFrame))
                || (g != null && g.buttonEast.wasPressedThisFrame);
        }

        private static bool UpPressed()
        {
            var k = Keyboard.current;
            var g = Gamepad.current;
            return (k != null && (k.wKey.wasPressedThisFrame || k.upArrowKey.wasPressedThisFrame))
                || (g != null && g.dpad.up.wasPressedThisFrame);
        }

        private static bool DownPressed()
        {
            var k = Keyboard.current;
            var g = Gamepad.current;
            return (k != null && (k.sKey.wasPressedThisFrame || k.downArrowKey.wasPressedThisFrame))
                || (g != null && g.dpad.down.wasPressedThisFrame);
        }
    }
}
