using System;
using System.Collections.Generic;
using StealthCastle.Isometric3D.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Dir = StealthCastle.Isometric3D.World.ConnectionPoint3D.Dir;
using RouteMode = StealthCastle.Isometric3D.World.PatrolRoute3D.RouteMode;

namespace StealthCastle.Isometric3D.EditorTools
{
    /// <summary>
    /// Стартовый набор: детали (Kit — страж, пропы, стены, выход, спавн + LevelKit3D), 15 кусков из этих деталей
    /// и игровая сцена с генератором. Куски — обычные префабы: дальше их правят руками (инспектор LevelPiece3D),
    /// новые создаются через StealthCastle → 3D → New Level Piece.
    /// Пересборка перезаписывает только стартовые куски (P01–P15), детали набора и игровую сцену;
    /// куски с другими именами не трогает и подключает к генератору вместе со стартовыми.
    /// </summary>
    public static class LevelPieces3DBuilder
    {
        private const string Root = "Assets/Prefabs/Isometric3D";
        private const string KitFolder = Root + "/Kit";
        private const string MaterialsFolder = Root + "/Materials";
        private const string KitAssetPath = Root + "/LevelKit3D.asset";
        private const string ScenePath = "Assets/Scenes/Isometric3D_Game.unity";
        private const string SceneName = "Isometric3D_Game";
        private const string PipelinePath = "Assets/Settings/UniversalRP.asset";
        private const string RendererPath = "Assets/Settings/UniversalRenderer3D.asset";
        private const string InputActionsPath = "Assets/InputSystem_Actions.inputactions";

        private const float WallHeight = PieceAuthoring3D.WallHeight;
        private const float BodyHeight = PieceAuthoring3D.BodyHeight;

        [MenuItem("StealthCastle/3D/Rebuild Starter Pieces + Game Scene")]
        public static void BuildFromMenu()
        {
            if (!EditorUtility.DisplayDialog("Пересобрать стартовый набор?",
                    "Будут перезаписаны детали набора (Kit), стартовые куски P01–P15 и сцена Isometric3D_Game.\n" +
                    "Куски с другими именами не трогаются.", "Пересобрать", "Отмена"))
            {
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build();
        }

        [MenuItem("StealthCastle/3D/Rebuild Starter Pieces Only")]
        public static void BuildPiecesFromMenu()
        {
            if (!EditorUtility.DisplayDialog("Пересобрать стартовые куски?",
                    "Будут перезаписаны куски P01–P15. Детали набора и игровая сцена не трогаются.", "Пересобрать", "Отмена"))
            {
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildPieces();
        }

        /// <summary>Только куски P01–P15 (набор деталей дополняется недостающим, сцена не трогается)</summary>
        public static void BuildPieces()
        {
            PieceAuthoring3D.EnsureFolder(PieceAuthoring3D.PiecesFolder);
            PieceAuthoring3D.EnsureFolder(PieceAuthoring3D.DataFolder);
            var scene = EditorSceneManager.GetActiveScene().path;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var kit = BuildKit(new BuildAssets());
            int count = 0;
            foreach (var spec in Specs())
            {
                BuildPiece(spec, kit);
                count++;
            }
            AssetDatabase.SaveAssets();
            if (!string.IsNullOrEmpty(scene)) EditorSceneManager.OpenScene(scene);
            Debug.Log($"[LevelPieces3DBuilder] {count} starter pieces rebuilt");
        }

        public static void Build()
        {
            PieceAuthoring3D.EnsureFolder(PieceAuthoring3D.PiecesFolder);
            PieceAuthoring3D.EnsureFolder(PieceAuthoring3D.DataFolder);
            PieceAuthoring3D.EnsureFolder(MaterialsFolder);
            PieceAuthoring3D.EnsureFolder(KitFolder);

            // Детали и куски собираются во временной сцене, чтобы не трогать открытую
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var assets = new BuildAssets();
            var kit = BuildKit(assets);

            int count = 0;
            foreach (var spec in Specs())
            {
                BuildPiece(spec, kit);
                count++;
            }

            BuildGameScene(assets);
            AssetDatabase.SaveAssets();
            Debug.Log($"[LevelPieces3DBuilder] Kit + {count} starter pieces + {ScenePath}");
        }

        // --- Каталог кусков ------------------------------------------------------------

        private sealed class PieceSpec
        {
            public string Id;
            public PieceData3D.Tag Tag;
            public Dir[] Doors;
            public Action<PieceBuilder> Fill;
        }

        private static IEnumerable<PieceSpec> Specs()
        {
            // Старт: безопасная комната — без стражей, пропы рядом для первой маскировки
            yield return new PieceSpec
            {
                Id = "P01_Start_Guardhouse", Tag = PieceData3D.Tag.Start, Doors = new[] { Dir.North, Dir.East },
                Fill = b =>
                {
                    b.Spawn(-3f, -3f);
                    b.Wall(0.5f, 1.5f, 5f, 0.5f);
                    b.Crate(-4.5f, -0.5f);
                    b.Barrel(2.5f, -4f);
                    b.Crate(2.5f, 2.8f);
                    b.Sack(-4.6f, 4.6f);
                    b.Keg(4.6f, -2.4f);
                    b.Table(2.8f, -1.6f);
                }
            };

            // Галерея: страж ходит по центру из конца в конец, бочки у стен — укрытие на время прохода
            yield return new PieceSpec
            {
                Id = "P02_Gallery_NS", Tag = PieceData3D.Tag.Empty, Doors = new[] { Dir.North, Dir.South },
                Fill = b =>
                {
                    b.Wall(-2.7f, 0f, 0.5f, 7f);
                    b.Wall(2.7f, 0f, 0.5f, 7f);
                    b.Barrel(1.7f, -1.5f);
                    b.Crate(-1.7f, 2f);
                    b.Barrel(-4.5f, -4.5f);
                    b.Guard(RouteMode.PingPong, new Vector2(0f, 4.2f), new Vector2(0f, -4.2f));
                }
            };

            yield return new PieceSpec
            {
                Id = "P03_Gallery_WE", Tag = PieceData3D.Tag.Empty, Doors = new[] { Dir.West, Dir.East },
                Fill = b =>
                {
                    b.Wall(0f, -2.7f, 7f, 0.5f);
                    b.Wall(0f, 2.7f, 7f, 0.5f);
                    b.Barrel(-1.5f, 1.7f);
                    b.Crate(2f, -1.7f);
                    b.Barrel(4.5f, 4.5f);
                    b.Guard(RouteMode.PingPong, new Vector2(4.2f, 0f), new Vector2(-4.2f, 0f));
                }
            };

            // Повороты: глухой блок в углу, страж обходит угол — за поворотом его не видно
            yield return new PieceSpec
            {
                Id = "P04_Corner_SE", Tag = PieceData3D.Tag.Empty, Doors = new[] { Dir.South, Dir.East },
                Fill = b =>
                {
                    b.Wall(-3f, 3f, 5f, 5f);
                    b.Barrel(4.3f, -4.3f);
                    b.Crate(-3f, -4.3f); // Сбоку от проёма: фартук 1,5 м у проёма свободен
                    b.Guard(RouteMode.PingPong, new Vector2(1.5f, -3.5f), new Vector2(1.5f, 1.8f), new Vector2(4.5f, 1.8f));
                }
            };

            yield return new PieceSpec
            {
                Id = "P05_Corner_NW", Tag = PieceData3D.Tag.Empty, Doors = new[] { Dir.North, Dir.West },
                Fill = b =>
                {
                    b.Wall(3f, -3f, 5f, 5f);
                    b.Barrel(-4.3f, 4.3f);
                    b.Crate(3f, 4.3f); // Сбоку от проёма: фартук 1,5 м у проёма свободен
                    b.Guard(RouteMode.PingPong, new Vector2(-1.5f, 3.5f), new Vector2(-1.5f, -1.8f), new Vector2(-4.5f, -1.8f));
                }
            };

            // Зал с колоннами: два пересекающихся маршрута, колонны режут конусы — проход по таймингу
            yield return new PieceSpec
            {
                Id = "P06_PillarHall", Tag = PieceData3D.Tag.Combat, Doors = new[] { Dir.North, Dir.East, Dir.South, Dir.West },
                Fill = b =>
                {
                    b.Pillar(-3f, -3f);
                    b.Pillar(3f, -3f);
                    b.Pillar(-3f, 3f);
                    b.Pillar(3f, 3f);
                    b.Barrel(3f, 1.6f);
                    b.Crate(-3f, -1.6f);
                    b.Barrel(-1.6f, 3f);
                    b.Guard(RouteMode.Loop, new Vector2(-4.5f, -4.5f), new Vector2(4.5f, -4.5f), new Vector2(4.5f, 4.5f), new Vector2(-4.5f, 4.5f));
                    b.Guard(RouteMode.PingPong, new Vector2(4.5f, 0f), new Vector2(-4.5f, 0f));
                }
            };

            // Склад: много ящиков — раздолье для маскировки, стеллажи делят зал на ряды
            yield return new PieceSpec
            {
                Id = "P07_Storage", Tag = PieceData3D.Tag.Combat, Doors = new[] { Dir.West, Dir.East, Dir.North },
                Fill = b =>
                {
                    b.Shelf(-2f, 2f, 3f, 0.6f);
                    b.Shelf(2f, -2f, 3f, 0.6f);
                    b.Sack(-1.5f, 3.1f);
                    b.Sack(1.6f, -3.1f);
                    b.Crate(-3.3f, 0f);
                    b.Crate(-1f, -0.3f);
                    b.Crate(2f, 0.3f);
                    b.Crate(3f, 2.8f);
                    b.Crate(-3f, -2.8f);
                    b.Barrel(0.5f, 3f);
                    b.Guard(RouteMode.Loop, new Vector2(-4.5f, -4f), new Vector2(4.5f, -4f), new Vector2(4.5f, 4f), new Vector2(-4.5f, 4f));
                }
            };

            // Библиотека (кусок-«сокровищница»): стеллажи, сундук, страж ходит по главному проходу
            yield return new PieceSpec
            {
                Id = "P08_Library", Tag = PieceData3D.Tag.Treasure, Doors = new[] { Dir.North, Dir.South },
                Fill = b =>
                {
                    b.Shelf(-3f, 2.5f, 4f, 0.6f);
                    b.Shelf(-3f, -2.5f, 4f, 0.6f);
                    b.Shelf(3f, 0f, 4f, 0.6f);
                    b.Barrel(-4.5f, 0f);
                    b.Crate(4.5f, -3.5f);
                    b.Crate(4.5f, 3.5f);
                    b.Chest(-4.6f, 4.6f);
                    b.Guard(RouteMode.PingPong, new Vector2(0f, -4.2f), new Vector2(0f, 4.2f));
                }
            };

            // Выход: проёмы со всех сторон (открыт один — тот, через который пришли), страж кружит у портала
            yield return new PieceSpec
            {
                Id = "P09_Exit_Portcullis", Tag = PieceData3D.Tag.Exit, Doors = new[] { Dir.North, Dir.East, Dir.South, Dir.West },
                Fill = b =>
                {
                    b.Exit(0f, 0f);
                    b.Barrel(-4.5f, -4.5f);
                    b.Crate(4.5f, 4.5f);
                    b.Crate(-4.5f, 4.5f);
                    b.Guard(RouteMode.Loop, new Vector2(-3f, -3f), new Vector2(3f, -3f), new Vector2(3f, 3f), new Vector2(-3f, 3f));
                }
            };

            // Ещё два поворота — полный набор: цепь может свернуть в любую сторону
            yield return new PieceSpec
            {
                Id = "P10_Corner_NE", Tag = PieceData3D.Tag.Empty, Doors = new[] { Dir.North, Dir.East },
                Fill = b =>
                {
                    b.Wall(-3f, -3f, 5f, 5f);
                    b.Barrel(4.3f, 4.3f);
                    b.Crate(-3f, 4.3f); // Сбоку от проёма: фартук 1,5 м у проёма свободен
                    b.Guard(RouteMode.PingPong, new Vector2(1.5f, 3.5f), new Vector2(1.5f, -1.8f), new Vector2(4.5f, -1.8f));
                }
            };

            yield return new PieceSpec
            {
                Id = "P11_Corner_SW", Tag = PieceData3D.Tag.Empty, Doors = new[] { Dir.South, Dir.West },
                Fill = b =>
                {
                    b.Wall(3f, 3f, 5f, 5f);
                    b.Barrel(-4.3f, -4.3f);
                    b.Crate(3f, -4.3f); // Сбоку от проёма: фартук 1,5 м у проёма свободен
                    b.Guard(RouteMode.PingPong, new Vector2(-1.5f, -3.5f), new Vector2(-1.5f, 1.8f), new Vector2(-4.5f, 1.8f));
                }
            };

            // Двор: фонтан в центре, два стража кружат вокруг навстречу друг другу — окно для прохода короткое
            yield return new PieceSpec
            {
                Id = "P12_Courtyard", Tag = PieceData3D.Tag.Combat, Doors = new[] { Dir.North, Dir.East, Dir.South, Dir.West },
                Fill = b =>
                {
                    b.LowWall(0f, 0f, 3f, 3f, 1.2f); // Чаша фонтана: ниже роста, но выше глаз стража
                    b.Pillar(-4.5f, -4.5f);
                    b.Pillar(4.5f, -4.5f);
                    b.Pillar(-4.5f, 4.5f);
                    b.Pillar(4.5f, 4.5f);
                    b.Crate(4.6f, -2.5f);
                    b.Barrel(-4.6f, 2.5f);
                    b.Crate(-2.5f, 4.6f);
                    b.Barrel(2.5f, -4.6f);
                    b.Guard(RouteMode.Loop, new Vector2(-3f, -3f), new Vector2(3f, -3f), new Vector2(3f, 3f), new Vector2(-3f, 3f));
                    b.Guard(RouteMode.Loop, new Vector2(3f, 3f), new Vector2(-3f, 3f), new Vector2(-3f, -3f), new Vector2(3f, -3f));
                }
            };

            // Казарма: ряды коек, часовой посреди прохода оглядывается — проскочить, пока смотрит в другую сторону
            yield return new PieceSpec
            {
                Id = "P13_Barracks", Tag = PieceData3D.Tag.Empty, Doors = new[] { Dir.North, Dir.South },
                Fill = b =>
                {
                    foreach (float z in new[] { -3f, -0.5f, 2f })
                    {
                        b.Furniture(-3.5f, z, 2.5f, 1f, 1f); // Койки
                        b.Furniture(3.5f, z, 2.5f, 1f, 1f);
                    }
                    b.Crate(-2.7f, -4.5f); // Сбоку от проёма: фартук 1,5 м у проёма свободен
                    b.Barrel(1.6f, 3.8f);
                    b.Barrel(-4.6f, 4.4f);
                    b.Guard(RouteMode.Loop, new Vector2(0f, 0.8f)); // Одна точка — часовой
                }
            };

            // Кухня: столы-острова и много бочек, страж ходит ломаной через весь зал
            yield return new PieceSpec
            {
                Id = "P14_Kitchen", Tag = PieceData3D.Tag.Combat, Doors = new[] { Dir.West, Dir.East },
                Fill = b =>
                {
                    b.Furniture(-2.5f, 2.5f, 2f, 1f, 1f); // Разделочные столы
                    b.Furniture(2.5f, 2.5f, 2f, 1f, 1f);
                    b.Furniture(-2.5f, -2.5f, 2f, 1f, 1f);
                    b.Furniture(2.5f, -2.5f, 2f, 1f, 1f);
                    b.Sack(-4.6f, -2.6f);
                    b.Keg(4.6f, 2.6f);
                    b.Barrel(-4.5f, 4.5f);
                    b.Barrel(4.5f, 4.5f);
                    b.Barrel(0f, 3f);
                    b.Crate(0f, -3f);
                    b.Crate(-4.5f, -4.5f);
                    b.Guard(RouteMode.PingPong, new Vector2(-4.5f, 0f), new Vector2(0f, 0f), new Vector2(4.5f, 0f), new Vector2(4.5f, -4.5f));
                }
            };

            // Хранилище (сокровищница): сундук в кольце колонн, патруль вокруг и часовой у сундука
            yield return new PieceSpec
            {
                Id = "P15_Vault", Tag = PieceData3D.Tag.Treasure, Doors = new[] { Dir.West, Dir.East },
                Fill = b =>
                {
                    b.Pillar(-2.5f, -2.5f);
                    b.Pillar(2.5f, -2.5f);
                    b.Pillar(-2.5f, 2.5f);
                    b.Pillar(2.5f, 2.5f);
                    b.Chest(0f, 0f);
                    b.Crate(-1f, 4.6f);
                    b.Crate(1f, -4.6f);
                    b.Barrel(4.6f, 4.6f);
                    b.Barrel(-4.6f, -4.6f);
                    b.Guard(RouteMode.Loop, new Vector2(-4f, -4f), new Vector2(4f, -4f), new Vector2(4f, 4f), new Vector2(-4f, 4f));
                    b.Guard(RouteMode.Loop, new Vector2(0f, 1.5f)); // Часовой у сундука
                }
            };
        }

        // --- Сборка куска ---------------------------------------------------------------

        private static void BuildPiece(PieceSpec spec, LevelKit3D kit)
        {
            var root = new GameObject(spec.Id);
            var piece = root.AddComponent<LevelPiece3D>();
            piece.Configure(spec.Tag, spec.Doors);
            PieceAuthoring3D.RebuildShell(piece, kit, recordUndo: false);
            spec.Fill(new PieceBuilder(piece, kit));

            var path = $"{PieceAuthoring3D.PiecesFolder}/{spec.Id}.prefab";
            PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);

            // Ссылка на данные сохраняется в самом префабе куска
            using (var editing = new PrefabUtility.EditPrefabContentsScope(path))
            {
                PieceAuthoring3D.SyncData(editing.prefabContentsRoot.GetComponent<LevelPiece3D>(), path);
            }
        }

        /// <summary>Наполнение куска в его локальных координатах (x, z), пол на y = 0</summary>
        private sealed class PieceBuilder
        {
            private readonly LevelPiece3D piece;
            private readonly LevelKit3D kit;

            public PieceBuilder(LevelPiece3D piece, LevelKit3D kit)
            {
                this.piece = piece;
                this.kit = kit;
            }

            /// <summary>Внутренняя каменная стена во всю высоту: блокирует движение и зрение</summary>
            public void Wall(float x, float z, float sizeX, float sizeZ) => Block("Wall_Inner", x, z, sizeX, sizeZ, WallHeight, WallBlock3D.Style.Stone);

            /// <summary>Невысокая каменная кладка (чаша фонтана, парапет)</summary>
            public void LowWall(float x, float z, float sizeX, float sizeZ, float height) => Block("Wall_Low", x, z, sizeX, sizeZ, height, WallBlock3D.Style.Stone);

            /// <summary>Деревянный стеллаж под 2.2 м</summary>
            public void Shelf(float x, float z, float sizeX, float sizeZ) => Block("Shelf", x, z, sizeX, sizeZ, 2.2f, WallBlock3D.Style.Wood);

            /// <summary>Стол, койка, прилавок: по пояс, но выше глаз стража (0.9 м) — укрытие</summary>
            public void Furniture(float x, float z, float sizeX, float sizeZ, float height) => Block("Furniture", x, z, sizeX, sizeZ, height, WallBlock3D.Style.Wood);

            private void Block(string name, float x, float z, float sizeX, float sizeZ, float height, WallBlock3D.Style style)
            {
                WallBlocks3D.Create(piece.transform, name, new Vector3(x, 0f, z), new Vector3(sizeX, height, sizeZ), style,
                    PieceAuthoring3D.LayerOrDefault(kit.wallLayer), recordUndo: false);
            }

            public void Pillar(float x, float z) => Part(kit.pillarPrefab, x, z);
            public void Crate(float x, float z) => Part(kit.cratePrefab, x, z);
            public void Barrel(float x, float z) => Part(kit.barrelPrefab, x, z);
            public void Chest(float x, float z) => Part(kit.chestPrefab, x, z);
            public void Spawn(float x, float z) => Part(kit.spawnPrefab, x, z);
            public void Exit(float x, float z) => Part(kit.exitPrefab, x, z);
            public void Sack(float x, float z) => Part(kit.sackPrefab, x, z);
            public void Keg(float x, float z) => Part(kit.kegPrefab, x, z);
            public void Table(float x, float z) => Part(kit.tablePrefab, x, z);

            /// <summary>Страж-экземпляр префаба из набора + маршрут; точки — по полу (x, z)</summary>
            public void Guard(RouteMode mode, params Vector2[] points) => PieceAuthoring3D.AddGuard(piece, kit, mode, points);

            private void Part(GameObject prefab, float x, float z)
            {
                if (prefab == null) return; // Деталь ещё не собрана (модели из Blender не применены)
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, piece.transform);
                go.transform.localPosition = new Vector3(x, 0f, z);
            }
        }

        // --- Набор деталей ------------------------------------------------------------------

        /// <summary>
        /// Префабы деталей с опорой на полу (кроме стража: его центр — на высоте 0.9 м) и ассет LevelKit3D
        /// </summary>
        private static LevelKit3D BuildKit(BuildAssets assets)
        {
            var kit = AssetDatabase.LoadAssetAtPath<LevelKit3D>(KitAssetPath);
            if (kit == null)
            {
                kit = ScriptableObject.CreateInstance<LevelKit3D>();
                AssetDatabase.CreateAsset(kit, KitAssetPath);
            }

            kit.wallMaterial = assets.WallMat;
            kit.blockerMaterial = assets.BlockerMat;
            kit.floorMaterials = new Material[Enum.GetValues(typeof(PieceData3D.Tag)).Length];
            foreach (PieceData3D.Tag tag in Enum.GetValues(typeof(PieceData3D.Tag)))
            {
                kit.floorMaterials[(int)tag] = assets.FloorFor(tag);
            }

            kit.cratePrefab = EnsurePrefab("Crate", () => PropRoot("Crate", "Ящик", PrimitiveType.Cube, new Vector3(0f, 0.5f, 0f), Vector3.one, assets.CrateMat, assets));
            kit.barrelPrefab = EnsurePrefab("Barrel", () => PropRoot("Barrel", "Бочка", PrimitiveType.Cylinder, new Vector3(0f, 0.6f, 0f), new Vector3(0.8f, 0.6f, 0.8f), assets.BarrelMat, assets));
            kit.wallPrefab = EnsurePrefab("Wall", () => WallRoot(assets), existing => existing.GetComponent<WallBlock3D>() == null);
            kit.pillarPrefab = EnsurePrefab("Pillar", () => SolidRoot("Pillar", new Vector3(1.2f, WallHeight, 1.2f), assets.WallMat, assets.WallLayer));
            kit.chestPrefab = EnsurePrefab("Chest", () => SolidRoot("Chest", new Vector3(1f, 0.7f, 0.7f), assets.GoldMat, assets.WallLayer));
            EnsureChestLoot(kit.chestPrefab);
            kit.exitPrefab = EnsurePrefab("LevelExit", () => ExitRoot(assets));
            kit.spawnPrefab = EnsurePrefab("PlayerSpawn", SpawnRoot);
            kit.guardPrefab = EnsurePrefab("Guard", () => GuardRoot(assets));

            EditorUtility.SetDirty(kit);
            AssetDatabase.SaveAssets();
            return kit;
        }

        /// <summary>
        /// Деталь создаётся, только если её ещё нет: настройки префаба (например, стража) пересборка не затирает
        /// </summary>
        /// <param name="outdated">Существующую деталь всё же пересоздать (сменилось устройство детали)</param>
        private static GameObject EnsurePrefab(string name, Func<GameObject> create, Func<GameObject, bool> outdated = null)
        {
            var path = $"{KitFolder}/{name}.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null && (outdated == null || !outdated(existing))) return existing;

            var root = create();
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        /// <summary>
        /// Сундук даёт добычу: компонент LootChest3D на корне префаба (добавляется и в уже существующий префаб)
        /// </summary>
        internal static void EnsureChestLoot(GameObject chestPrefab)
        {
            if (chestPrefab == null || chestPrefab.GetComponent<LootChest3D>() != null) return;
            var path = AssetDatabase.GetAssetPath(chestPrefab);
            using (var editing = new PrefabUtility.EditPrefabContentsScope(path))
            {
                var root = editing.prefabContentsRoot;
                var chest = root.AddComponent<LootChest3D>();
                SetField(chest, "bodyRenderer", p => p.objectReferenceValue = root.GetComponentInChildren<Renderer>());
            }
        }

        /// <summary>Проп: корень на полу, меш с компонентом маскировки — дочерний</summary>
        private static GameObject PropRoot(string name, string displayName, PrimitiveType type, Vector3 meshPosition, Vector3 meshScale,
            Material material, BuildAssets assets)
        {
            var root = new GameObject(name);
            var mesh = GameObject.CreatePrimitive(type);
            mesh.name = "Mesh";
            mesh.layer = assets.PropLayer;
            mesh.transform.SetParent(root.transform, false);
            mesh.transform.localPosition = meshPosition;
            mesh.transform.localScale = meshScale;
            mesh.GetComponent<Renderer>().sharedMaterial = material;
            mesh.AddComponent<Mechanics.DisguisableProp3D>().SetDisplayName(displayName);
            return root;
        }

        /// <summary>Глухая деталь (стена, колонна, сундук): корень на полу, куб — дочерний</summary>
        private static GameObject SolidRoot(string name, Vector3 size, Material material, int layer)
        {
            var root = new GameObject(name);
            var mesh = GameObject.CreatePrimitive(PrimitiveType.Cube);
            mesh.name = "Mesh";
            mesh.layer = layer;
            mesh.transform.SetParent(root.transform, false);
            mesh.transform.localPosition = new Vector3(0f, size.y * 0.5f, 0f);
            mesh.transform.localScale = size;
            mesh.GetComponent<Renderer>().sharedMaterial = material;
            return root;
        }

        /// <summary>Сегмент стены 2 м для ручной сборки кусков (размер правится в инспекторе стены)</summary>
        private static GameObject WallRoot(BuildAssets assets)
        {
            var holder = new GameObject("Holder");
            var wall = WallBlocks3D.Create(holder.transform, "Wall", Vector3.zero, new Vector3(2f, WallHeight, 0.5f),
                WallBlock3D.Style.Stone, assets.WallLayer, false).transform;
            wall.SetParent(null); // Стена — сам корень префаба
            UnityEngine.Object.DestroyImmediate(holder);
            return wall.gameObject;
        }

        private static GameObject ExitRoot(BuildAssets assets)
        {
            var exit = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            exit.name = "LevelExit";
            exit.transform.localScale = new Vector3(2f, 0.05f, 2f);
            exit.GetComponent<Renderer>().sharedMaterial = assets.ExitMat;
            // Триггер высокий, чтобы капсула игрока гарантированно вошла (цилиндр сплющен до 0.1 м)
            UnityEngine.Object.DestroyImmediate(exit.GetComponent<Collider>());
            var trigger = exit.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, 10f, 0f);
            trigger.size = new Vector3(1f, 20f, 1f);
            exit.AddComponent<LevelExit3D>();
            return exit;
        }

        private static GameObject SpawnRoot()
        {
            var spawn = new GameObject("PlayerSpawn");
            spawn.AddComponent<SpawnPoint3D>();
            return spawn;
        }

        /// <summary>Страж: все настройки поведения — на префабе; маршрут задаётся в куске (у каждого экземпляра свой)</summary>
        private static GameObject GuardRoot(BuildAssets assets)
        {
            var guard = new GameObject("Guard") { tag = "Enemy", layer = assets.EnemyLayer };

            // Триггер: поимка считается по дистанции, сплошные капсулы не дали бы подойти
            var collider = guard.AddComponent<CapsuleCollider>();
            collider.height = 1.8f;
            collider.radius = 0.4f;
            collider.isTrigger = true;

            var ai = guard.AddComponent<Enemies.EnemyAI3D>();
            guard.AddComponent<Mechanics.NoiseReceiver3D>();
            var cone = guard.AddComponent<Enemies.VisionCone3D>();
            SetField(cone, "coneMaterial", p => p.objectReferenceValue = assets.ConeMat);

            var visual = assets.CreateBody("Visual", assets.EnemyMat).transform;
            visual.SetParent(guard.transform, false);

            int wallMask = 1 << assets.WallLayer;
            var so = new SerializedObject(ai);
            so.FindProperty("visualTransform").objectReferenceValue = visual;
            so.FindProperty("wallMask").intValue = wallMask;
            so.FindProperty("obstacleMask").intValue = wallMask;
            so.FindProperty("visionRange").floatValue = 6f;   // GDD: конус 6 м
            so.FindProperty("fovDegrees").floatValue = 70f;
            so.FindProperty("chaseSpeed").floatValue = 5.5f;  // Быстрее шага (5), медленнее бега (8)
            so.ApplyModifiedPropertiesWithoutUndo();
            return guard;
        }

        // --- Игровая сцена ------------------------------------------------------------------

        private static void BuildGameScene(BuildAssets assets)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var lightGo = new GameObject("Directional Light");
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            light.shadows = LightShadows.Soft;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.35f, 0.35f, 0.4f);

            // Менеджер распознаёт игровую сцену по имени и сразу начинает забег
            var gameManager = new GameObject("GameManager3D").AddComponent<Core.GameManager3D>();
            SetField(gameManager, "gameScene", p => p.stringValue = SceneName);
            SetField(gameManager, "hubScene", p => p.stringValue = HubScene3DBuilder.SceneName);

            var inputSystem = new GameObject("InputSystem3D").AddComponent<Core.InputSystem3D>();
            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            SetField(inputSystem, "inputActions", p => p.objectReferenceValue = actions);

            // Все куски проекта — и стартовые, и собранные руками
            var generator = new GameObject("LevelGenerator3D").AddComponent<LevelGenerator3D>();
            var generatorSo = new SerializedObject(generator);
            generatorSo.FindProperty("wallMask").intValue = 1 << assets.WallLayer;
            generatorSo.ApplyModifiedPropertiesWithoutUndo();
            LevelGenerator3DEditor.Collect(new SerializedObject(generator));

            var player = CreatePlayer(assets);
            CreateCamera(player.transform);
            new GameObject("UI").AddComponent<Setup.UISetup3D>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings(ScenePath);
        }

        /// <param name="stealthKit">Маскировка и шум — для игровой сцены; в логове они не нужны (E там — взаимодействие)</param>
        internal static GameObject CreatePlayer(BuildAssets assets, bool stealthKit = true)
        {
            // Генератор переносит игрока в точку спавна стартового куска
            var player = new GameObject("Player3D") { tag = "Player", layer = assets.PlayerLayer };
            player.transform.position = new Vector3(0f, BodyHeight, 0f);

            var collider = player.AddComponent<CapsuleCollider>();
            collider.height = 1.8f;
            collider.radius = 0.4f;
            var controller = player.AddComponent<Player.PlayerController3D>();
            if (stealthKit) player.AddComponent<Mechanics.DisguiseSystem3D>();
            var disguiseVisual = player.AddComponent<Player.PlayerDisguiseVisual3D>();
            if (stealthKit) player.AddComponent<Mechanics.NoiseEmitter3D>();

            var visual = new GameObject("PlayerVisual").transform;
            visual.SetParent(player.transform, false);
            SetField(controller, "visualTransform", p => p.objectReferenceValue = visual);
            SetField(disguiseVisual, "modelContainer", p => p.objectReferenceValue = visual);
            SetField(disguiseVisual, "defaultPlayerModel", p => p.objectReferenceValue = assets.PlayerModelPrefab);
            return player;
        }

        internal static void CreateCamera(Transform target, float zoom = 22f)
        {
            var cameraGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = cameraGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.08f, 0.08f, 0.1f);
            cameraGo.AddComponent<AudioListener>();
            camera.GetUniversalAdditionalCameraData().SetRenderer(FindUniversalRendererIndex());

            var cameraSystem = cameraGo.AddComponent<Core.CameraSystem3D>();
            var so = new SerializedObject(cameraSystem);
            so.FindProperty("target").objectReferenceValue = target;
            so.FindProperty("rotationAngle").floatValue = 50f;
            so.FindProperty("zoomDistance").floatValue = zoom;
            so.FindProperty("fieldOfView").floatValue = 35f;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Индекс Universal Renderer в URP-ассете (добавляет TestScene3DBuilder)</summary>
        private static int FindUniversalRendererIndex()
        {
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            var rendererData = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(RendererPath);
            var list = new SerializedObject(pipeline).FindProperty("m_RendererDataList");
            for (int i = 0; i < list.arraySize; i++)
            {
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == rendererData) return i;
            }
            throw new InvalidOperationException("Universal Renderer not found — run StealthCastle → 3D → Build Test Scene once");
        }

        // --- Материалы ----------------------------------------------------------------------

        /// <summary>Материалы, слои и префаб модели игрока</summary>
        internal sealed class BuildAssets
        {
            public readonly int GroundLayer = Layer("Ground");
            public readonly int WallLayer = Layer("Wall");
            public readonly int PropLayer = Layer("Prop");
            public readonly int PlayerLayer = Layer("Player");
            public readonly int EnemyLayer = Layer("Enemy");

            public readonly Material WallMat = Lit("Wall", new Color(0.55f, 0.53f, 0.5f));
            public readonly Material BlockerMat = Lit("Blocker", new Color(0.46f, 0.44f, 0.42f));
            public readonly Material CrateMat = Lit("Crate", new Color(0.6f, 0.42f, 0.2f));
            public readonly Material BarrelMat = Lit("Barrel", new Color(0.45f, 0.27f, 0.12f));
            public readonly Material GoldMat = Lit("Gold", new Color(0.9f, 0.7f, 0.2f));
            public readonly Material ExitMat = Lit("Exit", new Color(0.2f, 0.85f, 0.3f));
            public readonly Material PlayerMat = Lit("Player", new Color(0.25f, 0.45f, 0.9f));
            public readonly Material EnemyMat = Lit("Enemy", new Color(0.85f, 0.25f, 0.2f));
            public readonly Material MarkerMat = Lit("FacingMarker", new Color(0.95f, 0.95f, 0.9f));
            public readonly Material ConeMat = VisionCone();
            public readonly GameObject PlayerModelPrefab;

            private readonly Dictionary<PieceData3D.Tag, Material> floors = new Dictionary<PieceData3D.Tag, Material>();

            public BuildAssets()
            {
                foreach (PieceData3D.Tag tag in Enum.GetValues(typeof(PieceData3D.Tag)))
                {
                    floors[tag] = Lit($"Floor_{tag}", FloorColor(tag));
                }
                PlayerModelPrefab = CreateBodyPrefab("PlayerModel", PlayerMat);
            }

            public Material FloorFor(PieceData3D.Tag tag) => floors[tag];

            /// <summary>Цвет пола по тегу — видно ещё при сборке уровня, куда ведёт путь</summary>
            private static Color FloorColor(PieceData3D.Tag tag) => tag switch
            {
                PieceData3D.Tag.Start => new Color(0.3f, 0.42f, 0.35f),
                PieceData3D.Tag.Exit => new Color(0.36f, 0.3f, 0.48f),
                PieceData3D.Tag.Treasure => new Color(0.46f, 0.4f, 0.24f),
                PieceData3D.Tag.Combat => new Color(0.42f, 0.29f, 0.29f),
                _ => new Color(0.3f, 0.3f, 0.34f),
            };

            /// <summary>Капсула-тело с меткой «лица», чтобы было видно, куда смотрит персонаж</summary>
            public GameObject CreateBody(string name, Material bodyMat)
            {
                var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                body.name = name;
                UnityEngine.Object.DestroyImmediate(body.GetComponent<Collider>());
                body.transform.localScale = new Vector3(0.8f, 0.9f, 0.8f);
                body.GetComponent<Renderer>().sharedMaterial = bodyMat;

                var marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
                marker.name = "FacingMarker";
                UnityEngine.Object.DestroyImmediate(marker.GetComponent<Collider>());
                marker.transform.SetParent(body.transform, false);
                marker.transform.localPosition = new Vector3(0f, 0.45f, 0.45f);
                marker.transform.localScale = new Vector3(0.5f, 0.12f, 0.2f);
                marker.GetComponent<Renderer>().sharedMaterial = MarkerMat;
                return body;
            }

            private GameObject CreateBodyPrefab(string name, Material bodyMat)
            {
                var path = $"{Root}/{name}.prefab";
                var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (existing != null) return existing;

                // PlayerDisguiseVisual3D сбрасывает localScale модели в 1 — масштаб держим на дочернем теле
                var root = new GameObject(name);
                CreateBody("Body", bodyMat).transform.SetParent(root.transform, false);
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
                UnityEngine.Object.DestroyImmediate(root);
                return prefab;
            }

            internal static Material Lit(string name, Color color)
            {
                var path = $"{MaterialsFolder}/{name}.mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    // Цвет задаётся только новому материалу — правки в проекте не затираются
                    material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = color };
                    AssetDatabase.CreateAsset(material, path);
                }
                return material;
            }

            private static Material VisionCone()
            {
                var path = $"{MaterialsFolder}/VisionCone.mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    material = Enemies.VisionCone3D.CreateDefaultMaterial();
                    AssetDatabase.CreateAsset(material, path);
                }
                return material;
            }
        }

        // --- Утилиты ------------------------------------------------------------------------

        internal static void SetField(UnityEngine.Object target, string field, Action<SerializedProperty> assign)
        {
            var so = new SerializedObject(target);
            var property = so.FindProperty(field) ?? throw new InvalidOperationException($"{target.GetType().Name}.{field} not found");
            assign(property);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static int Layer(string layerName)
        {
            int layer = LayerMask.NameToLayer(layerName);
            if (layer < 0)
            {
                Debug.LogWarning($"[LevelPieces3DBuilder] Layer '{layerName}' not found, using Default");
                return 0;
            }
            return layer;
        }

        internal static void AddSceneToBuildSettings(string path)
        {
            var scenes = EditorBuildSettings.scenes;
            foreach (var s in scenes)
            {
                if (s.path == path) return;
            }
            var updated = new EditorBuildSettingsScene[scenes.Length + 1];
            scenes.CopyTo(updated, 0);
            updated[scenes.Length] = new EditorBuildSettingsScene(path, true);
            EditorBuildSettings.scenes = updated;
        }
    }
}
