using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using StealthCastle.Isometric;
using StealthCastle.Isometric.World.Editor;
using StealthCastle.Isometric.World;
using StealthCastle.Isometric.Core;
using StealthCastle.Isometric.UI;
using StealthCastle.Isometric.Mechanics;
using StealthCastle.Isometric.Enemies;
using StealthCastle.Isometric.Player;
using StealthCastle.Debugging;

/// <summary>Editor-хелпер: запись в приватное [SerializeField] поле через SerializedObject.</summary>
public static class EditorFieldWriter
{
    public static void Write(Object target, string fieldName, object value)
    {
        var so = new SerializedObject(target);
        var prop = so.FindProperty(fieldName);
        if (prop != null)
        {
            if (value is Object obj) prop.objectReferenceValue = obj;
            else if (value is string s) prop.stringValue = s;
            else if (value is float f) prop.floatValue = f;
            else if (value is int i) prop.intValue = i;
            else if (value is bool b) prop.boolValue = b;
            else if (value is System.Collections.IList list)
            {
                prop.ClearArray();
                foreach (var item in list)
                {
                    var idx = prop.arraySize;
                    prop.InsertArrayElementAtIndex(idx);
                    prop.GetArrayElementAtIndex(idx).objectReferenceValue = item as Object;
                }
            }
            so.ApplyModifiedProperties();
        }
        else
        {
            UnityEngine.Debug.LogWarning($"[EditorFieldWriter] Поле '{fieldName}' не найдено у {target.GetType().Name}");
        }
    }
}

/// <summary>
/// Точка входа для Editor-инструментов: пересборка кусков + создание MVP-префабов и Sandbox-сцены.
/// </summary>
public static class RebuildPrefabsBatch
{
    [MenuItem("Tools/RebuildPrefabsBatch")]
    public static void Run()
    {
        UnityEngine.Debug.Log("[RebuildPrefabsBatch] Старт пересборки префабов...");
        IsoPiecePrefabFactory.CreateAll();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        UnityEngine.Debug.Log("[RebuildPrefabsBatch] Готово.");
    }

    // ============================================================================================
    // Меню: собрать MVP-префабы (игрок, враг, пропсы) + Sandbox-сцену для ручных тестов с визуалом
    // ============================================================================================
    [MenuItem("Tools/MVP/Создать TMP-кнопку (префаб)")]
    public static void CreateTmpButtonPrefab()
    {
        var folder = "Assets/Prefabs/Prototype_ISO";
        System.IO.Directory.CreateDirectory(folder);
        var path = folder + "/ButtonTMP.prefab";

        // Корневой GameObject кнопки
        var root = new GameObject("ButtonTMP");
        var rt = root.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(200f, 40f);

        // Image (фон кнопки)
        var img = root.AddComponent<UnityEngine.UI.Image>();
        img.color = new Color32(200, 200, 200, 255);

        // Button
        var btn = root.AddComponent<UnityEngine.UI.Button>();
        btn.targetGraphic = img;
        var colorBlock = btn.colors;
        colorBlock.normalColor = new Color32(200, 200, 200, 255);
        colorBlock.highlightedColor = new Color32(230, 230, 230, 255);
        colorBlock.pressedColor = new Color32(180, 180, 180, 255);
        btn.colors = colorBlock;

        // Текст (TextMeshProUGUI)
        var txtGo = new GameObject("Text");
        txtGo.transform.SetParent(root.transform, false);
        var txtRt = txtGo.AddComponent<RectTransform>();
        txtRt.anchorMin = Vector2.zero;
        txtRt.anchorMax = Vector2.one;
        txtRt.sizeDelta = Vector2.zero;
        txtRt.pivot = new Vector2(0.5f, 0.5f);
        var txt = txtGo.AddComponent<TextMeshProUGUI>();
        txt.text = "Кнопка";
        txt.alignment = TextAlignmentOptions.Center;
        txt.fontSize = 14f;
        txt.color = Color.black;
        // TMP требует fontAsset; берём дефолтный из TMP Settings
        txt.font = TMPro.TMP_Settings.defaultFontAsset;

        PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        UnityEngine.Debug.Log($"[MVP] TMP-кнопка создана: {path}");
    }

    [MenuItem("Tools/MVP/Собрать префабы и Sandbox-сцену")]
    public static void BuildMvpPrefabsAndSandbox()
    {
        BuildMvpPrefabs();
        BuildSandboxScene();
        UnityEngine.Debug.Log("[MVP] Префабы и Sandbox-сцена готовы.");
    }

    [MenuItem("Tools/MVP/Собрать префабы")]
    public static void BuildMvpPrefabs()
    {
        MvpPrefabBuilder.BuildAll();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    [MenuItem("Tools/MVP/Создать Sandbox-сцену")]
    public static void BuildSandboxScene()
    {
        MvpSceneBuilder.BuildSandbox();
    }
}

/// <summary>
/// Собирает готовые к перетаскиванию префабы для изометрического MVP:
/// IsoPlayer, IsoGuard, Barrel, Crate, Statue, Chest.
/// </summary>
public static class MvpPrefabBuilder
{
    public const string Folder = "Assets/Prefabs/Iso";

    [MenuItem("Tools/MVP/Собрать префабы")]
    public static void BuildAll()
    {
        System.IO.Directory.CreateDirectory(Folder);
        System.IO.Directory.CreateDirectory(Folder + "/Player");
        System.IO.Directory.CreateDirectory(Folder + "/Enemies");
        System.IO.Directory.CreateDirectory(Folder + "/Props");

        var playerLayer = LayerMask.NameToLayer("Player");
        var enemyLayer = LayerMask.NameToLayer("Enemy");
        var propLayer = LayerMask.NameToLayer("Prop");
        if (playerLayer < 0) playerLayer = 0;
        if (enemyLayer < 0) enemyLayer = 0;
        if (propLayer < 0) propLayer = 0;

        BuildPlayer(playerLayer);
        BuildGuard(enemyLayer, propLayer);
        BuildProp("Barrel", new Color32(150, 120, 90, 255), propLayer, new Vector2(0.45f, 0.45f));
        BuildProp("Crate", new Color32(180, 140, 80, 255), propLayer, new Vector2(0.5f, 0.5f));
        BuildProp("Statue", new Color32(160, 160, 170, 255), propLayer, new Vector2(0.4f, 0.7f));
        BuildProp("Chest", new Color32(210, 180, 60, 255), propLayer, new Vector2(0.6f, 0.45f), isTreasure: true);

        UnityEngine.Debug.Log($"[MvpPrefabBuilder] Созданы префабы в {Folder}");
    }

    /// <summary>Игрок: Rigidbody2D + IsoPlayerController + IsoDisguiseSystem + IsoSilentKill + визуал.</summary>
    private static void BuildPlayer(int layer)
    {
        var root = new GameObject("IsoPlayer");
        root.layer = layer;
        root.transform.position = new Vector3(0f, 0f, -1f);

        var rb = root.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        var col = root.AddComponent<CircleCollider2D>();
        col.radius = 0.35f;

        var sr = root.AddComponent<SpriteRenderer>();
        sr.sprite = StealthCastle.Debugging.PlaceholderSpriteFactory.CreateSquareSprite(new Color32(220, 200, 120, 255));
        sr.sortingOrder = 5;

        var player = root.AddComponent<StealthCastle.Isometric.Player.IsoPlayerController>();
        var disguise = root.AddComponent<StealthCastle.Isometric.Mechanics.IsoDisguiseSystem>();
        var silentKill = root.AddComponent<StealthCastle.Isometric.Mechanics.IsoSilentKill>();
        var visual = root.AddComponent<StealthCastle.Isometric.Player.IsoPlayerDisguiseVisual>();

        // Дочерний рендерер маскировки (создаётся автоматически в Awake, но явно создадим для наглядности)
        var disguiseGo = new GameObject("DisguiseVisual");
        disguiseGo.transform.SetParent(root.transform, false);
        var disguiseSr = disguiseGo.AddComponent<SpriteRenderer>();
        disguiseSr.sortingOrder = 6;
        disguiseSr.gameObject.SetActive(false);
        // Привязка через SerializedObject (поля private [SerializeField])
        EditorFieldWriter.Write(visual, "mainSpriteRenderer", sr);
        EditorFieldWriter.Write(visual, "disguiseSpriteRenderer", disguiseSr);

        // Настройки IsoDisguiseSystem: таймер дыхания 6 сек, скан 2 м
        EditorFieldWriter.Write(disguise, "disguiseDuration", 6f);
        EditorFieldWriter.Write(disguise, "scanRadius", 2f);

        PrefabUtility.SaveAsPrefabAsset(root, $"{Folder}/Player/IsoPlayer.prefab");
        Object.DestroyImmediate(root);
    }

    /// <summary>Враг: Rigidbody2D + IsoEnemyAI + визуал + VisionRoot + EnemyVision.</summary>
    private static void BuildGuard(int enemyLayer, int wallLayer)
    {
        var root = new GameObject("IsoGuard");
        root.layer = enemyLayer;
        root.transform.position = new Vector3(0f, 0f, -1f);
        root.transform.localScale = new Vector3(0.8f, 0.8f, 1f);

        var rb = root.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;

        var ai = root.AddComponent<StealthCastle.Isometric.Enemies.IsoEnemyAI>();
        ai.WallMask = 1 << wallLayer;

        // Визуал врага (дочерний, для flipX)
        var visual = new GameObject("Visual");
        visual.transform.SetParent(root.transform, false);
        var esr = visual.AddComponent<SpriteRenderer>();
        esr.sprite = StealthCastle.Debugging.PlaceholderSpriteFactory.CreateSquareSprite(new Color32(200, 60, 60, 255));
        esr.sortingOrder = 5;
        EditorFieldWriter.Write(ai, "guardSprite", esr);

        // VisionRoot: pivot в центре, вращается по IVisionSource
        var visionRoot = new GameObject("VisionRoot");
        visionRoot.transform.SetParent(root.transform, false);
        visionRoot.transform.localPosition = Vector3.zero;
        visionRoot.AddComponent<StealthCastle.Isometric.Enemies.EnemyVision>();

        var zoneSprite = new GameObject("ZoneSprite");
        zoneSprite.transform.SetParent(visionRoot.transform, false);
        var zsr = zoneSprite.AddComponent<SpriteRenderer>();
        zsr.color = new Color(1f, 1f, 0f, 0.15f);
        zsr.sortingOrder = -5;
        zsr.sprite = StealthCastle.Debugging.PlaceholderSpriteFactory.CreateWhiteSprite();

        // 4 waypoint'а по углам комнаты 12 м (локальные ±5)
        var wpField = new SerializedObject(ai).FindProperty("waypoints");
        wpField.arraySize = 4;
        wpField.GetArrayElementAtIndex(0).vector3Value = new Vector3(-5f, -5f, 0);
        wpField.GetArrayElementAtIndex(1).vector3Value = new Vector3(5f, -5f, 0);
        wpField.GetArrayElementAtIndex(2).vector3Value = new Vector3(5f, 5f, 0);
        wpField.GetArrayElementAtIndex(3).vector3Value = new Vector3(-5f, 5f, 0);
        wpField.serializedObject.ApplyModifiedProperties();

        PrefabUtility.SaveAsPrefabAsset(root, $"{Folder}/Enemies/IsoGuard.prefab");
        Object.DestroyImmediate(root);
    }

    /// <summary>Проп: Sprite + CircleCollider2D (trigger) + IsoDisguisableProp + IsoDisguiseHighlight.</summary>
    private static void BuildProp(string name, Color32 color, int propLayer, Vector2 size, bool isTreasure = false)
    {
        var root = new GameObject("Prop_" + name);
        root.layer = propLayer;

        var col = root.AddComponent<CircleCollider2D>();
        col.radius = Mathf.Max(size.x, size.y) * 0.5f;
        col.isTrigger = true;

        var sr = root.AddComponent<SpriteRenderer>();
        sr.sprite = StealthCastle.Debugging.PlaceholderSpriteFactory.CreateSquareSprite(color);
        sr.color = color;
        sr.sortingOrder = 0;

        var prop = root.AddComponent<StealthCastle.Isometric.Mechanics.IsoDisguisableProp>();
        EditorFieldWriter.Write(prop, "spriteRenderer", sr);
        EditorFieldWriter.Write(prop, "isTreasure", isTreasure);

        // Outline
        var highlight = root.AddComponent<StealthCastle.Isometric.Mechanics.IsoDisguiseHighlight>();
        EditorFieldWriter.Write(highlight, "outlineScale", 1.08f);

        PrefabUtility.SaveAsPrefabAsset(root, $"{Folder}/Props/Prop_{name}.prefab");
        Object.DestroyImmediate(root);
    }
}

/// <summary>
/// Создаёт Sandbox-сцену для ручных тестов концепта с визуалом:
/// камера, свет, игрок, пара кусков, враг, пропсы, UI (BreathTimer + ArtifactSelector),
/// RunManager + LevelGenerator. Всё собирается из префабов — можно редактировать вручную.
/// </summary>
public static class MvpSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/Sandbox.unity";

    [MenuItem("Tools/MVP/Создать Sandbox-сцену")]
    public static void BuildSandbox()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // 1. Камера + свет
        var cam = CreateCamera();
        var light = new GameObject("Directional Light");
        light.AddComponent<Light>().type = LightType.Directional;
        light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        // 2. RunManager + LevelGenerator (ядро цикла)
        var genRoot = new GameObject("LevelGen");
        var generator = genRoot.AddComponent<StealthCastle.Isometric.World.LevelGenerator>();
        var runManager = genRoot.AddComponent<StealthCastle.Isometric.Core.RunManager>();

        // 3. UI: Canvas + BreathTimer + ArtifactSelector (полная связка)
        var canvas = CreateCanvas();
        var breathTimer = CreateBreathTimer(canvas);
        var rules = CreateGenerationRules();
        var selector = CreateArtifactSelector(canvas, rules);

        // 4. Связки RunManager
        EditorFieldWriter.Write(runManager, "levelGenerator", generator);
        // playerController привяжется после создания игрока
        EditorFieldWriter.Write(runManager, "artifactSelector", selector);

        // 5. Игрок (префаб) — телепортируем в центр стартового куска
        var player = CreatePlayerFromPrefab();
        if (player != null)
        {
            EditorFieldWriter.Write(runManager, "playerController", player.GetComponent<StealthCastle.Isometric.Player.IsoPlayerController>());
            EditorFieldWriter.Write(breathTimer, "player", player);
            cam.GetComponent<StealthCastle.Isometric.Core.IsoCameraFollow>()?.SetTarget(player.transform);
        }

        // 6. Стартовый кусок вручную (чтобы сразу видеть визуал)
        var startPiece = CreatePieceAt("Piece_Start", Vector3.zero);
        var corridor = CreatePieceAt("Piece_CombatHall", new Vector3(0f, 10f, 0f));
        var exitPiece = CreatePieceAt("Piece_ExitHall", new Vector3(0f, 20f, 0f));

        // 7. Враг в Combat-куске (префаб)
        var guard = CreateGuardFromPrefab(new Vector3(0f, 12f, 0f));

        // 8. Пропсы для тренировки маскировки
        CreatePropFromPrefab("Barrel", new Vector3(1.5f, 14f, 0f));
        CreatePropFromPrefab("Crate", new Vector3(-1.5f, 16f, 0f));
        CreatePropFromPrefab("Statue", new Vector3(2f, 18f, 0f));
        CreatePropFromPrefab("Chest", new Vector3(0f, 19f, 0f));

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();

        UnityEngine.Debug.Log($"[MvpSceneBuilder] Sandbox-сцена создана: {ScenePath}");
        UnityEngine.Debug.Log("[MvpSceneBuilder] Готово: жми Play. WASD — ходить, Interact (удерживать E) — скан маскировки, ЛКМ — атака.");
    }

    private static GameObject CreateCamera()
    {
        var cam = Camera.main;
        if (cam == null)
        {
            var camGo = new GameObject("Main Camera");
            cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 9f;
            cam.backgroundColor = new Color32(20, 20, 30, 255);
            cam.transform.position = new Vector3(0f, 0f, -10f);
        }

        // Изометрическая сортировка по Y (как в IsoBootstrap)
        cam.transparencySortMode = TransparencySortMode.CustomAxis;
        cam.transparencySortAxis = new Vector3(0f, 1f, 0f);

        // Камера следит за игроком (IsoCameraFollow)
        var follow = cam.gameObject.GetComponent<StealthCastle.Isometric.Core.IsoCameraFollow>()
                     ?? cam.gameObject.AddComponent<StealthCastle.Isometric.Core.IsoCameraFollow>();
        follow.SetTarget(GameObject.FindGameObjectWithTag("Player")?.transform);

        return cam.gameObject;
    }

    private static GameObject CreateCanvas()
    {
        var canvasGo = new GameObject("Canvas");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = Camera.main;
        canvasGo.AddComponent<CanvasScaler>();
        canvasGo.AddComponent<GraphicRaycaster>();
        canvasGo.layer = LayerMask.NameToLayer("UI");
        return canvasGo;
    }

    /// <summary>Создать BreathTimer: фон-полоска + заполнение, привязанный к игроку.</summary>
    private static StealthCastle.Isometric.UI.BreathTimer CreateBreathTimer(GameObject canvas)
    {
        var root = new GameObject("BreathTimer");
        root.transform.SetParent(canvas.transform, false);
        var rt = root.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -40f);
        rt.sizeDelta = new Vector2(200f, 20f);
        root.AddComponent<CanvasRenderer>();

        // Фон полоски
        var bg = new GameObject("Background");
        bg.transform.SetParent(root.transform, false);
        var bgRt = bg.AddComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.sizeDelta = Vector2.zero;
        var bgImg = bg.AddComponent<UnityEngine.UI.Image>();
        bgImg.color = new Color32(60, 60, 80, 255);

        // Заполнение (fillRect для BreathTimer)
        var fill = new GameObject("Fill");
        fill.transform.SetParent(root.transform, false);
        var fillRt = fill.AddComponent<RectTransform>();
        fillRt.anchorMin = new Vector2(0f, 0f);
        fillRt.anchorMax = new Vector2(1f, 1f);
        fillRt.sizeDelta = Vector2.zero;
        var fillImg = fill.AddComponent<UnityEngine.UI.Image>();
        fillImg.color = new Color32(100, 200, 140, 255);

        var canvasGroup = root.AddComponent<CanvasGroup>();

        var breathTimer = root.AddComponent<StealthCastle.Isometric.UI.BreathTimer>();
        EditorFieldWriter.Write(breathTimer, "fillRect", fillRt);
        EditorFieldWriter.Write(breathTimer, "canvasGroup", canvasGroup);
        // player привяжется снаружи после создания игрока

        return breathTimer;
    }

    /// <summary>Создать ArtifactSelector: список кнопок + контейнер.</summary>
    private static ArtifactSelector CreateArtifactSelector(GameObject canvas, GenerationRules rules)
    {
        var root = new GameObject("ArtifactSelector");
        root.transform.SetParent(canvas.transform, false);
        var rt = root.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0.5f);
        rt.anchorMax = new Vector2(0f, 0.5f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.anchoredPosition = new Vector2(40f, 0f);
        rt.sizeDelta = new Vector2(260f, 300f);
        root.AddComponent<CanvasRenderer>();

        var layout = root.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.spacing = 10f;

        var selector = root.AddComponent<StealthCastle.Isometric.UI.ArtifactSelector>();
        EditorFieldWriter.Write(selector, "buttonsContainer", rt);

        // Контейнер кнопок (дочерний)
        var container = new GameObject("Buttons");
        container.transform.SetParent(root.transform, false);
        var containerRt = container.AddComponent<RectTransform>();
        containerRt.anchorMin = Vector2.zero;
        containerRt.anchorMax = Vector2.one;
        containerRt.sizeDelta = new Vector2(-20f, 0f);
        EditorFieldWriter.Write(selector, "buttonsContainer", containerRt);

        // Кнопка-префаб
        var btnPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Prototype_ISO/Button.prefab");
        if (btnPrefab != null)
            EditorFieldWriter.Write(selector, "buttonPrefab", btnPrefab);

        // Артефакты (3 штуки, MVP) — создаём ассеты на диск
        var art1 = CreateArtifactAsset("Артефакт_Жадный_взгляд", "Больше сокровищниц: комнаты с сокровищами встречаются в 3 раза чаще", PieceData.Tag.Treasure, 3f);
        var art2 = CreateArtifactAsset("Артефакт_Тихий_путь", "Меньше врагов: боевые комнаты встречаются в 2 раза реже", PieceData.Tag.Combat, 0.5f);
        var art3 = CreateArtifactAsset("Артефакт_Острый_клинок", "Дольше в маскировке: дыхание задерживается на 3 сек больше", PieceData.Tag.Empty, 1f, disguiseBonus: 3f);

        var artifacts = new System.Collections.Generic.List<ArtifactData> { art1, art2, art3 };
        EditorFieldWriter.Write(selector, "availableArtifacts", artifacts);
        EditorFieldWriter.Write(selector, "rules", rules);

        return selector;
    }

    /// <summary>Создать GenerationRules с пулом из 6 кусков.</summary>
    private static GenerationRules CreateGenerationRules()
    {
        var rules = ScriptableObject.CreateInstance<GenerationRules>();
        rules.name = "SandboxGenerationRules";

        var pool = new System.Collections.Generic.List<PieceData>();
        var names = new[] { "Piece_Start_Data", "Piece_CorridorStraight_Data", "Piece_CorridorL_Data",
                            "Piece_CombatHall_Data", "Piece_TreasureRoom_Data", "Piece_ExitHall_Data" };
        foreach (var n in names)
        {
            var data = AssetDatabase.LoadAssetAtPath<PieceData>($"Assets/Prefabs/World/IsoPieces/{n}.asset");
            if (data != null) pool.Add(data);
            else UnityEngine.Debug.LogWarning($"[MvpSceneBuilder] PieceData не найден: {n}");
        }

        // piecePool — List<UnityEngine.Object>, пишем через SerializedObject напрямую
        var so = new SerializedObject(rules);
        var poolProp = so.FindProperty("piecePool");
        poolProp.ClearArray();
        foreach (var p in pool)
        {
            var idx = poolProp.arraySize;
            poolProp.InsertArrayElementAtIndex(idx);
            poolProp.GetArrayElementAtIndex(idx).objectReferenceValue = p;
        }
        so.ApplyModifiedProperties();

        AssetDatabase.CreateAsset(rules, "Assets/Scripts/Isometric/Core/SandboxGenerationRules.asset");
        return rules;
    }

    /// <summary>Создать ассет артефакта на диск (с бонусом маскировки для «Острого клинка»).</summary>
    private static ArtifactData CreateArtifactAsset(string name, string desc,
        PieceData.Tag tag, float mult, float disguiseBonus = 0f)
    {
        var art = ScriptableObject.CreateInstance<ArtifactData>();
        art.name = name;
        EditorFieldWriter.Write(art, "displayName", name.Replace("Артефакт_", ""));
        EditorFieldWriter.Write(art, "description", desc);

        // affectedTag — enum, пишем через SerializedObject
        var so = new SerializedObject(art);
        so.FindProperty("affectedTag").enumValueIndex = (int)tag;
        so.FindProperty("weightMultiplier").floatValue = mult;
        so.FindProperty("enemySpeedBonus").floatValue = 0f;
        if (disguiseBonus > 0f)
            so.FindProperty("disguiseDurationBonus").floatValue = disguiseBonus;
        so.ApplyModifiedProperties();

        var path = $"Assets/Scripts/Isometric/Core/{name.Replace(' ', '_')}.asset";
        AssetDatabase.CreateAsset(art, path);
        return art;
    }

    private static GameObject CreatePlayerFromPrefab()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{MvpPrefabBuilder.Folder}/Player/IsoPlayer.prefab");
        if (prefab == null)
        {
            UnityEngine.Debug.LogError("[MvpSceneBuilder] Не найден IsoPlayer.prefab — сначала выполни Tools/MVP/Собрать префабы");
            return null;
        }
        var go = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        go.transform.position = new Vector3(0f, 0f, -1f);
        return go;
    }

    private static GameObject CreateGuardFromPrefab(Vector3 pos)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{MvpPrefabBuilder.Folder}/Enemies/IsoGuard.prefab");
        if (prefab == null)
        {
            UnityEngine.Debug.LogError("[MvpSceneBuilder] Не найден IsoGuard.prefab");
            return null;
        }
        var go = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        go.transform.position = pos;
        return go;
    }

    private static void CreatePropFromPrefab(string name, Vector3 pos)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{MvpPrefabBuilder.Folder}/Props/Prop_{name}.prefab");
        if (prefab == null) return;
        var go = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        go.transform.position = pos;
    }

    private static GameObject CreatePieceAt(string pieceName, Vector3 pos)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/World/IsoPieces/{pieceName}.prefab");
        if (prefab == null)
        {
            UnityEngine.Debug.LogWarning($"[MvpSceneBuilder] Кусок {pieceName} не найден");
            return null;
        }
        var go = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        go.transform.position = pos;
        go.SetActive(true);
        return go;
    }
}
