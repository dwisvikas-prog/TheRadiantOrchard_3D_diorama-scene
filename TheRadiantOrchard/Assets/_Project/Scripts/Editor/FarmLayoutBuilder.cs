using System.Collections.Generic;
using System.IO;
using RadiantOrchard.App;
using RadiantOrchard.Core;
using RadiantOrchard.Presentation;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace RadiantOrchard.Editor
{
    /// <summary>
    /// Lays out the placeholder farm in the Game scene from FarmDefinition: zone ground tilemaps, path, 9 plots,
    /// Wisdom Tree, Stone Well, bench and the visitor path points, then wires FarmView into the GameInstaller.
    /// Re-running replaces the previous "FarmGrid". Placeholder art only; real sprites replace it later.
    /// </summary>
    public static class FarmLayoutBuilder
    {
        private const string GameScenePath = "Assets/_Project/Scenes/Game.unity";
        private const string GameConfigPath = "Assets/_Project/Data/GameConfig.asset";
        private const string FarmFolder = "Assets/_Project/Data/Farm";
        private const string FarmAssetPath = FarmFolder + "/FarmDefinition.asset";
        private const string PlaceholderFolder = "Assets/_Project/Art/Sprites/Placeholders";
        private const string PlotSpritePath = "Assets/_Project/Art/Sprites/Plots/plot_empty.png";
        private const string SoftDotPath = "Assets/_Project/Art/Sprites/Placeholders/ph_soft_dot.png";
        private const string UnlitSpriteMaterialPath = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";
        private const string TimerFontPath = "Assets/_Project/Art/Fonts/Fredoka-Bold SDF.asset";
        private const string Renderer2DPath = "Assets/_Project/Settings/Renderer2D.asset";
        private const string RootName = "FarmGrid";

        private static readonly Vector3 CellSize = new Vector3(2.56f, 1.28f, 1f);
        private const int GroundOrder = -100;
        private const int PathOrder = -90;

        // Painted colour of each zone's placeholder ground; the desaturation shader shows it grey until the zone is restored.
        private static readonly Color[] ZoneColours =
        {
            Hex("#8DBF5A"), Hex("#7DB35E"), Hex("#9CC566"), Hex("#86B86A"), Hex("#74A956"),
        };
        // How much darker the grey (withered) ground is than the plain grey of its colour.
        private const float ZoneDarken = 0.12f;
        private static readonly Color PathColour = Hex("#B8A88E");
        private static readonly Color LockedPlotTint = new Color(0.55f, 0.55f, 0.55f, 1f);
        private static readonly Color TimerBackground = Hex("#FFF6E3");
        private static readonly Color TimerInk = Hex("#3B2A1E");

        [MenuItem("Radiant Orchard/Setup/Build Placeholder Farm")]
        public static void Build()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                if (SceneManager.GetSceneAt(i).isDirty)
                {
                    Debug.LogError("FarmLayoutBuilder: save or discard the open scene's changes first; nothing was changed.");
                    return;
                }
            }

            EnsureFolder(FarmFolder);
            EnsureFolder(PlaceholderFolder);
            Sprite tileSprite = EnsureSprite("ph_tile.png", MakeDiamond(256, 128), new Vector2(0.5f, 0.5f));
            Sprite blockSprite = EnsureSprite("ph_block.png", MakeSquare(64), new Vector2(0.5f, 0f));
            Tile groundTile = EnsureTile("ph_ground_tile.asset", tileSprite);
            UseCustomSortAxis();

            Scene game = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);

            // Load assets after opening the scene: opening a scene unloads unused assets.
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(GameConfigPath);
            var plotSprite = AssetDatabase.LoadAssetAtPath<Sprite>(PlotSpritePath);
            tileSprite = AssetDatabase.LoadAssetAtPath<Sprite>(PlaceholderFolder + "/ph_tile.png");
            blockSprite = AssetDatabase.LoadAssetAtPath<Sprite>(PlaceholderFolder + "/ph_block.png");
            groundTile = AssetDatabase.LoadAssetAtPath<Tile>(PlaceholderFolder + "/ph_ground_tile.asset");
            var timerFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TimerFontPath);
            if (config == null || config.Balance == null || plotSprite == null || timerFont == null)
            {
                Debug.LogError("FarmLayoutBuilder: GameConfig (with Balance), the plot_empty sprite or the Fredoka-Bold SDF font is missing.");
                return;
            }
            FarmDefinition farm = EnsureFarmDefinition(config);

            foreach (GameObject root in game.GetRootGameObjects())
                if (root.name == RootName) Object.DestroyImmediate(root);

            var rootGo = new GameObject(RootName);
            var grid = rootGo.AddComponent<Grid>();
            grid.cellLayout = GridLayout.CellLayout.Isometric;
            grid.cellSize = CellSize;
            var view = rootGo.AddComponent<FarmView>();

            Material desaturate = DesaturateMaterial.Ensure();
            if (desaturate == null) return;
            Tilemap[] zones = BuildZones(rootGo.transform, farm, groundTile, desaturate, out Desaturation[] zoneColours);
            BuildPath(rootGo.transform, farm, groundTile);
            PlotView[] plots = BuildPlots(rootGo.transform, grid, farm, plotSprite, blockSprite, tileSprite, timerFont, config.Balance.PlotsOpenAt(0));

            Transform landmarks = Child(rootGo.transform, "Landmarks");
            Transform tree = Landmark(landmarks, grid, farm.WisdomTreeCell, "WisdomTree", blockSprite, new Vector2(1.8f, 6f), Hex("#6E8B5A"), "Wisdom Tree");
            WisdomTreeView treeView = AddTreeGlow(tree, new Vector2(1.8f, 6f));
            if (treeView == null) return;
            AddTreeBloom(treeView, new Vector2(1.8f, 6f), config.Fruits.Count);
            Transform well = Landmark(landmarks, grid, farm.WellCell, "StoneWell", blockSprite, new Vector2(1.6f, 1.4f), Hex("#9AA39A"), "Stone Well");
            Transform bench = Landmark(landmarks, grid, farm.BenchCell, "Bench", blockSprite, new Vector2(2.6f, 0.6f), Hex("#8A5A35"), "Bench (3 seats)");

            Transform path = Child(rootGo.transform, "VisitorPath");
            Transform spawn = Marker(path, CellPoint(grid, farm.SpawnPoint), "Spawn", tileSprite, Hex("#C25B5B"), "Spawn");
            var spots = new Transform[farm.WaitingSpotCells.Count];
            for (int i = 0; i < spots.Length; i++)
                spots[i] = Marker(path, CellCentre(grid, farm.WaitingSpotCells[i]), "WaitingSpot_" + (i + 1), tileSprite, Hex("#E0C36A"), "Spot " + (i + 1));
            Transform exit = Marker(path, CellPoint(grid, farm.ExitPoint), "Exit", tileSprite, Hex("#5B8FC2"), "Exit");

            var so = new SerializedObject(view);
            so.FindProperty("_grid").objectReferenceValue = grid;
            SetArray(so.FindProperty("_plots"), plots);
            so.FindProperty("_wisdomTree").objectReferenceValue = tree;
            so.FindProperty("_wisdomTreeView").objectReferenceValue = treeView;
            so.FindProperty("_well").objectReferenceValue = well;
            so.FindProperty("_bench").objectReferenceValue = bench;
            so.FindProperty("_spawnPoint").objectReferenceValue = spawn;
            SetArray(so.FindProperty("_waitingSpots"), spots);
            so.FindProperty("_exitPoint").objectReferenceValue = exit;
            SetPoints(so.FindProperty("_entryRoute"), grid, farm.EntryRoute);
            SetPoints(so.FindProperty("_exitRoute"), grid, farm.ExitRoute);
            SetArray(so.FindProperty("_zones"), zones);
            SetArray(so.FindProperty("_zoneColours"), zoneColours);
            so.FindProperty("_worldBounds").rectValue = WorldBounds(grid, farm);
            so.ApplyModifiedPropertiesWithoutUndo();

            GameInstaller installer = null;
            foreach (GameObject root in game.GetRootGameObjects())
                if (installer == null) installer = root.GetComponentInChildren<GameInstaller>(true);
            if (installer == null)
            {
                Debug.LogError("FarmLayoutBuilder: no GameInstaller in Game; run Radiant Orchard > Setup > Add Bootstrapper and GameInstaller first.");
                return;
            }
            var installerSo = new SerializedObject(installer);
            installerSo.FindProperty("_farmView").objectReferenceValue = view;
            installerSo.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(game);
            EditorSceneManager.SaveScene(game);
            Debug.Log($"FarmLayoutBuilder: built {plots.Length} plots, {zones.Length} zones, {spots.Length} waiting spots; camera bounds {view.WorldBounds}.");
        }

        // Each zone is its own tilemap on the desaturation material, starting grey (CLAUDE.md 5.1).
        private static Tilemap[] BuildZones(Transform parent, FarmDefinition farm, Tile tile, Material desaturate, out Desaturation[] colours)
        {
            Transform ground = Child(parent, "Ground");
            var zones = new Tilemap[farm.Zones.Count];
            colours = new Desaturation[zones.Length];
            for (int z = 0; z < zones.Length; z++)
            {
                Tilemap map = NewTilemap(ground, "Zone_" + (z + 1), GroundOrder);
                var renderer = map.GetComponent<TilemapRenderer>();
                renderer.sharedMaterial = desaturate;
                Color colour = ZoneColours[z % ZoneColours.Length];
                foreach (Vector2Int cell in farm.Zones[z].Cells())
                {
                    var pos = new Vector3Int(cell.x, cell.y, 0);
                    map.SetTile(pos, tile);
                    // Checkerboard so the grid stays visible on placeholder ground.
                    map.SetColor(pos, ((cell.x + cell.y) & 1) == 0 ? colour : new Color(colour.r * 0.94f, colour.g * 0.94f, colour.b * 0.94f, 1f));
                }
                zones[z] = map;

                var zoneColour = map.gameObject.AddComponent<Desaturation>();
                var so = new SerializedObject(zoneColour);
                SetArray(so.FindProperty("_renderers"), new Object[] { renderer });
                so.FindProperty("_saturation").floatValue = 0f;
                so.FindProperty("_darken").floatValue = ZoneDarken;
                so.ApplyModifiedPropertiesWithoutUndo();
                colours[z] = zoneColour;
            }
            return zones;
        }

        // The path bands (layout v2: a crossroads around the centre square), reaching into the jungle where visitors come and go.
        private static void BuildPath(Transform parent, FarmDefinition farm, Tile tile)
        {
            Tilemap map = NewTilemap(parent, "PathTiles", PathOrder);
            foreach (RectInt band in farm.PathCells)
            {
                foreach (Vector2Int cell in band.allPositionsWithin)
                {
                    var pos = new Vector3Int(cell.x, cell.y, 0);
                    map.SetTile(pos, tile);
                    map.SetColor(pos, PathColour);
                }
            }
        }

        // A point given in cell coordinates (may be fractional, e.g. the line between two path cells) in world space.
        private static Vector3 CellPoint(Grid grid, Vector2 cell) =>
            grid.transform.TransformPoint(grid.CellToLocalInterpolated(new Vector3(cell.x, cell.y, 0f)));

        private static Vector3 CellCentre(Grid grid, Vector2Int cell) => grid.GetCellCenterWorld(new Vector3Int(cell.x, cell.y, 0));

        private static void SetPoints(SerializedProperty array, Grid grid, IReadOnlyList<Vector2> cells)
        {
            array.arraySize = cells.Count;
            for (int i = 0; i < cells.Count; i++) array.GetArrayElementAtIndex(i).vector2Value = CellPoint(grid, cells[i]);
        }

        private static PlotView[] BuildPlots(Transform parent, Grid grid, FarmDefinition farm, Sprite plotSprite, Sprite block, Sprite tile, TMP_FontAsset timerFont, int openAtStart)
        {
            Transform container = Child(parent, "Plots");
            var plots = new PlotView[farm.PlotCells.Count];
            // Centre of the plot's cell block, relative to its bottom corner (isometric: x = (w-h)/2 * cellW, y = (w+h)/2 * cellH).
            Vector2Int size = farm.PlotSizeCells;
            var centre = new Vector3((size.x - size.y) * 0.5f * CellSize.x * 0.5f, (size.x + size.y) * 0.5f * CellSize.y * 0.5f, 0f);

            for (int i = 0; i < plots.Length; i++)
            {
                var root = new GameObject("Plot_" + (i + 1).ToString("00"));
                root.transform.SetParent(container, false);
                Vector2Int cell = farm.PlotCells[i];
                root.transform.position = grid.CellToWorld(new Vector3Int(cell.x, cell.y, 0));
                // The plot and the tree planted on it sort as one object, by the plot's bottom corner.
                root.AddComponent<SortingGroup>();

                var soil = Renderer(root.transform, "Soil", plotSprite, 0, Vector3.zero);
                soil.color = i < openAtStart ? Color.white : LockedPlotTint;
                var tree = Renderer(root.transform, "Tree", block, 1, centre);
                tree.enabled = false;
                var fruit = Renderer(root.transform, "Fruit", tile, 2, centre + new Vector3(0f, 2.2f, 0f));
                fruit.transform.localScale = new Vector3(0.35f, 0.35f, 1f);
                fruit.enabled = false;
                // Above the fully grown placeholder tree (2.6 tall), so the countdown never hides behind it.
                (GameObject timer, TMP_Text timerText) = Timer(root.transform, block, timerFont, centre + new Vector3(0f, 3.1f, 0f));

                var view = root.AddComponent<PlotView>();
                var so = new SerializedObject(view);
                so.FindProperty("_soil").objectReferenceValue = soil;
                so.FindProperty("_tree").objectReferenceValue = tree;
                so.FindProperty("_fruit").objectReferenceValue = fruit;
                so.FindProperty("_timer").objectReferenceValue = timer;
                so.FindProperty("_timerText").objectReferenceValue = timerText;
                so.ApplyModifiedPropertiesWithoutUndo();
                plots[i] = view;
            }
            return plots;
        }

        // Placeholder countdown pill: a cream block with world-space text, hidden until a tree is growing.
        private static (GameObject, TMP_Text) Timer(Transform parent, Sprite block, TMP_FontAsset font, Vector3 localPosition)
        {
            var root = new GameObject("Timer");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = localPosition;

            var background = Renderer(root.transform, "Background", block, 3, new Vector3(0f, -0.3f, 0f));
            background.transform.localScale = new Vector3(1.9f / (block.rect.width / block.pixelsPerUnit), 0.6f / (block.rect.height / block.pixelsPerUnit), 1f);
            background.color = TimerBackground;

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(root.transform, false);
            var text = textGo.AddComponent<TextMeshPro>();
            text.font = font;
            text.text = string.Empty;
            text.fontSize = 5f;
            text.color = TimerInk;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.rectTransform.sizeDelta = new Vector2(1.9f, 0.6f);
            text.sortingOrder = 4;

            root.SetActive(false);
            return (root, text);
        }

        private static SpriteRenderer Renderer(Transform parent, string name, Sprite sprite, int order, Vector3 localPosition)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            return sr;
        }

        // The gold glow behind the Wisdom Tree while a quiz waits (CLAUDE.md 5.8): a soft dot stretched around the trunk.
        private static WisdomTreeView AddTreeGlow(Transform tree, Vector2 treeSizeWorld)
        {
            var dot = AssetDatabase.LoadAssetAtPath<Sprite>(SoftDotPath);
            var unlit = AssetDatabase.LoadAssetAtPath<Material>(UnlitSpriteMaterialPath);
            if (dot == null || unlit == null)
            {
                Debug.LogError($"FarmLayoutBuilder: missing {SoftDotPath} (run Build Tonic) or {UnlitSpriteMaterialPath}.");
                return null;
            }

            var glowGo = new GameObject("QuizGlow");
            glowGo.transform.SetParent(tree, false);
            glowGo.transform.localPosition = new Vector3(0f, treeSizeWorld.y * 0.5f, 0f);
            Vector2 dotSize = dot.bounds.size;
            glowGo.transform.localScale = new Vector3(treeSizeWorld.x * 2.2f / dotSize.x, treeSizeWorld.y * 1.25f / dotSize.y, 1f);
            var glow = glowGo.AddComponent<SpriteRenderer>();
            glow.sprite = dot;
            glow.sharedMaterial = unlit;
            glow.color = new Color(1f, 0.84f, 0.35f, 0.6f);
            // Behind the trunk inside the tree's Sorting Group; off until a quiz waits.
            glow.sortingOrder = -1;
            glow.enabled = false;

            var view = tree.gameObject.AddComponent<WisdomTreeView>();
            var so = new SerializedObject(view);
            so.FindProperty("_body").objectReferenceValue = tree.Find("PlaceholderBody").GetComponent<SpriteRenderer>();
            so.FindProperty("_glow").objectReferenceValue = glow;
            so.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        // The bloom (CLAUDE.md 5.9, LM-03 placeholder): a pink crown of soft dots over the trunk with one fruit spot per
        // fruit in two staggered rows, built hidden. Fruits are soft dots painted at runtime from each fruit's icon or tint.
        private static void AddTreeBloom(WisdomTreeView view, Vector2 treeSizeWorld, int fruitCount)
        {
            var dot = AssetDatabase.LoadAssetAtPath<Sprite>(SoftDotPath);
            var unlit = AssetDatabase.LoadAssetAtPath<Material>(UnlitSpriteMaterialPath);
            Vector2 dotSize = dot.bounds.size;

            var bloom = new GameObject("Bloom").transform;
            bloom.SetParent(view.transform, false);
            // Low on the trunk: the tree stands at the top of the farm, and at the default zoom the meter pill covers
            // its top, where the fruits could not be tapped.
            bloom.localPosition = new Vector3(0f, treeSizeWorld.y * 0.45f, 0f);

            SpriteRenderer Dot(string name, Vector2 position, Vector2 size, Color colour, int order)
            {
                var go = new GameObject(name);
                go.transform.SetParent(bloom, false);
                go.transform.localPosition = position;
                go.transform.localScale = new Vector3(size.x / dotSize.x, size.y / dotSize.y, 1f);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = dot;
                sr.sharedMaterial = unlit;
                sr.color = colour;
                sr.sortingOrder = order;
                return sr;
            }

            SpriteRenderer crown = Dot("Crown", Vector2.zero, new Vector2(5.2f, 3.6f), Hex("#F4B8C8"), 1);
            Color petal = Hex("#FFE3EC");
            Dot("Blossom_L", new Vector2(-1.7f, -0.3f), new Vector2(1.8f, 1.5f), petal, 2);
            Dot("Blossom_R", new Vector2(1.7f, -0.2f), new Vector2(1.8f, 1.5f), petal, 2);
            Dot("Blossom_T", new Vector2(0.1f, 1.2f), new Vector2(2f, 1.4f), petal, 2);

            // Two rows (5 on top, 4 below) spaced wider than a fingertip at the default zoom.
            var fruits = new SpriteRenderer[fruitCount];
            int top = (fruitCount + 1) / 2;
            for (int i = 0; i < fruitCount; i++)
            {
                bool upper = i < top;
                int inRow = upper ? i : i - top;
                int rowCount = upper ? top : fruitCount - top;
                float x = (inRow - (rowCount - 1) * 0.5f) * 0.95f;
                fruits[i] = Dot("Fruit_" + (i + 1), new Vector2(x, upper ? 0.55f : -0.55f), new Vector2(0.7f, 0.7f), Color.white, 3);
            }
            bloom.gameObject.SetActive(false);

            var so = new SerializedObject(view);
            so.FindProperty("_bloom").objectReferenceValue = bloom;
            so.FindProperty("_crown").objectReferenceValue = crown;
            SetArray(so.FindProperty("_fruits"), fruits);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Transform Landmark(Transform parent, Grid grid, Vector2Int cell, string name, Sprite block, Vector2 sizeWorld, Color colour, string label)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = grid.GetCellCenterWorld(new Vector3Int(cell.x, cell.y, 0));
            go.AddComponent<SortingGroup>();

            var body = new GameObject("PlaceholderBody");
            body.transform.SetParent(go.transform, false);
            float unit = block.rect.width / block.pixelsPerUnit;
            body.transform.localScale = new Vector3(sizeWorld.x / unit, sizeWorld.y / unit, 1f);
            var sr = body.AddComponent<SpriteRenderer>();
            sr.sprite = block;
            sr.color = colour;

            Label(go.transform, label + "\n(placeholder)", sizeWorld.y + 0.5f);
            return go.transform;
        }

        private static Transform Marker(Transform parent, Vector3 position, string name, Sprite tile, Color colour, string label)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;

            // Marker visuals are editor-only helpers; the point itself (this transform) is what systems use.
            var visual = new GameObject("MarkerVisual") { tag = "EditorOnly" };
            visual.transform.SetParent(go.transform, false);
            visual.transform.localScale = new Vector3(0.45f, 0.45f, 1f);
            var sr = visual.AddComponent<SpriteRenderer>();
            sr.sprite = tile;
            sr.color = colour;
            sr.sortingOrder = PathOrder + 1;

            Label(go.transform, label, 0.6f);
            return go.transform;
        }

        // Scene-view-only name; a TextMeshPro label would write glyphs into the dynamic font asset on every open.
        private static void Label(Transform parent, string text, float height)
        {
            parent.gameObject.AddComponent<EditorLabel>().Set(text, height);
        }

        private static Tilemap NewTilemap(Transform parent, string name, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var map = go.AddComponent<Tilemap>();
            map.tileAnchor = new Vector3(0.5f, 0.5f, 0f);
            var renderer = go.AddComponent<TilemapRenderer>();
            renderer.sortingOrder = order;
            return map;
        }

        private static Rect WorldBounds(Grid grid, FarmDefinition farm)
        {
            RectInt cells = farm.FarmCells;
            Vector3[] corners =
            {
                grid.CellToWorld(new Vector3Int(cells.xMin, cells.yMin, 0)),
                grid.CellToWorld(new Vector3Int(cells.xMax, cells.yMin, 0)),
                grid.CellToWorld(new Vector3Int(cells.xMin, cells.yMax, 0)),
                grid.CellToWorld(new Vector3Int(cells.xMax, cells.yMax, 0)),
            };
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            foreach (Vector3 c in corners)
            {
                minX = Mathf.Min(minX, c.x); maxX = Mathf.Max(maxX, c.x);
                minY = Mathf.Min(minY, c.y); maxY = Mathf.Max(maxY, c.y);
            }
            float m = farm.CameraMarginWorld;
            return Rect.MinMaxRect(minX - m, minY - m, maxX + m, maxY + m);
        }

        private static FarmDefinition EnsureFarmDefinition(GameConfig config)
        {
            var farm = AssetDatabase.LoadAssetAtPath<FarmDefinition>(FarmAssetPath);
            if (farm == null)
            {
                farm = ScriptableObject.CreateInstance<FarmDefinition>();
                AssetDatabase.CreateAsset(farm, FarmAssetPath);
            }
            if (config.Farm != farm)
            {
                var so = new SerializedObject(config);
                so.FindProperty("_farm").objectReferenceValue = farm;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(config);
                AssetDatabase.SaveAssets();
            }
            return farm;
        }

        private static void UseCustomSortAxis()
        {
            var renderer = AssetDatabase.LoadMainAssetAtPath(Renderer2DPath);
            if (renderer == null)
            {
                Debug.LogError($"FarmLayoutBuilder: no 2D renderer at {Renderer2DPath}.");
                return;
            }
            var so = new SerializedObject(renderer);
            so.FindProperty("m_TransparencySortMode").intValue = (int)TransparencySortMode.CustomAxis;
            so.FindProperty("m_TransparencySortAxis").vector3Value = new Vector3(0f, 1f, 0f);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(renderer);
            AssetDatabase.SaveAssets();
        }

        private static Sprite EnsureSprite(string fileName, Texture2D texture, Vector2 pivot)
        {
            string path = PlaceholderFolder + "/" + fileName;
            if (!File.Exists(path))
            {
                File.WriteAllBytes(path, texture.EncodeToPNG());
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 100;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteAlignment = (int)SpriteAlignment.Custom;
                settings.spritePivot = pivot;
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);
                importer.SaveAndReimport();
            }
            Object.DestroyImmediate(texture);
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static Tile EnsureTile(string fileName, Sprite sprite)
        {
            string path = PlaceholderFolder + "/" + fileName;
            var tile = AssetDatabase.LoadAssetAtPath<Tile>(path);
            if (tile == null)
            {
                tile = ScriptableObject.CreateInstance<Tile>();
                tile.sprite = sprite;
                tile.flags = TileFlags.None; // lets each cell take its own colour
                tile.colliderType = Tile.ColliderType.None;
                AssetDatabase.CreateAsset(tile, path);
                AssetDatabase.SaveAssets();
            }
            return tile;
        }

        private static Texture2D MakeDiamond(int width, int height)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var pixels = new Color32[width * height];
            float cx = (width - 1) * 0.5f, cy = (height - 1) * 0.5f;
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                float d = Mathf.Abs(x - cx) / (width * 0.5f) + Mathf.Abs(y - cy) / (height * 0.5f);
                byte a = (byte)(Mathf.Clamp01((1f - d) * width * 0.25f) * 255f);
                pixels[y * width + x] = new Color32(255, 255, 255, a);
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        private static Texture2D MakeSquare(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, 255);
            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        private static Transform Child(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        private static void SetArray(SerializedProperty array, Object[] items)
        {
            array.arraySize = items.Length;
            for (int i = 0; i < items.Length; i++)
                array.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        private static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out Color c);
            return c;
        }
    }
}
