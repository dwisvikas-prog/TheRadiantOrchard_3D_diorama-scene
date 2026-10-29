using System.IO;
using RadiantOrchard.App;
using RadiantOrchard.Core;
using RadiantOrchard.Presentation;
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
        private const string Renderer2DPath = "Assets/_Project/Settings/Renderer2D.asset";
        private const string RootName = "FarmGrid";

        private static readonly Vector3 CellSize = new Vector3(2.56f, 1.28f, 1f);
        private const int GroundOrder = -100;
        private const int PathOrder = -90;

        private static readonly Color[] ZoneGreys =
        {
            Hex("#9C9C92"), Hex("#8F8F86"), Hex("#A6A69B"), Hex("#94948A"), Hex("#878780"),
        };
        private static readonly Color PathColour = Hex("#B8A88E");
        private static readonly Color LockedPlotTint = new Color(0.55f, 0.55f, 0.55f, 1f);

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
            if (config == null || config.Balance == null || plotSprite == null)
            {
                Debug.LogError("FarmLayoutBuilder: GameConfig (with Balance) or plot_empty sprite is missing.");
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

            Tilemap[] zones = BuildZones(rootGo.transform, farm, groundTile);
            BuildPath(rootGo.transform, farm, groundTile);
            Transform[] plots = BuildPlots(rootGo.transform, grid, farm, plotSprite, config.Balance.PlotsOpenAt(0));

            Transform landmarks = Child(rootGo.transform, "Landmarks");
            Transform tree = Landmark(landmarks, grid, farm.WisdomTreeCell, "WisdomTree", blockSprite, new Vector2(1.8f, 6f), Hex("#6E8B5A"), "Wisdom Tree");
            Transform well = Landmark(landmarks, grid, farm.WellCell, "StoneWell", blockSprite, new Vector2(1.6f, 1.4f), Hex("#9AA39A"), "Stone Well");
            Transform bench = Landmark(landmarks, grid, farm.BenchCell, "Bench", blockSprite, new Vector2(2.6f, 0.6f), Hex("#8A5A35"), "Bench (3 seats)");

            Transform path = Child(rootGo.transform, "VisitorPath");
            Transform spawn = Marker(path, grid, farm.SpawnCell, "Spawn", tileSprite, Hex("#C25B5B"), "Spawn");
            var spots = new Transform[farm.WaitingSpotCells.Count];
            for (int i = 0; i < spots.Length; i++)
                spots[i] = Marker(path, grid, farm.WaitingSpotCells[i], "WaitingSpot_" + (i + 1), tileSprite, Hex("#E0C36A"), "Spot " + (i + 1));
            Transform exit = Marker(path, grid, farm.ExitCell, "Exit", tileSprite, Hex("#5B8FC2"), "Exit");

            var so = new SerializedObject(view);
            so.FindProperty("_grid").objectReferenceValue = grid;
            SetArray(so.FindProperty("_plots"), plots);
            so.FindProperty("_wisdomTree").objectReferenceValue = tree;
            so.FindProperty("_well").objectReferenceValue = well;
            so.FindProperty("_bench").objectReferenceValue = bench;
            so.FindProperty("_spawnPoint").objectReferenceValue = spawn;
            SetArray(so.FindProperty("_waitingSpots"), spots);
            so.FindProperty("_exitPoint").objectReferenceValue = exit;
            SetArray(so.FindProperty("_zones"), zones);
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

        private static Tilemap[] BuildZones(Transform parent, FarmDefinition farm, Tile tile)
        {
            Transform ground = Child(parent, "Ground");
            var zones = new Tilemap[farm.ZoneCells.Count];
            for (int z = 0; z < zones.Length; z++)
            {
                Tilemap map = NewTilemap(ground, "Zone_" + (z + 1), GroundOrder);
                RectInt rect = farm.ZoneCells[z];
                Color grey = ZoneGreys[z % ZoneGreys.Length];
                foreach (Vector2Int cell in rect.allPositionsWithin)
                {
                    var pos = new Vector3Int(cell.x, cell.y, 0);
                    map.SetTile(pos, tile);
                    // Checkerboard so the grid stays visible on placeholder ground.
                    map.SetColor(pos, ((cell.x + cell.y) & 1) == 0 ? grey : new Color(grey.r * 0.94f, grey.g * 0.94f, grey.b * 0.94f, 1f));
                }
                zones[z] = map;
            }
            return zones;
        }

        private static void BuildPath(Transform parent, FarmDefinition farm, Tile tile)
        {
            Tilemap map = NewTilemap(parent, "PathTiles", PathOrder);
            Vector2Int from = farm.SpawnCell, to = farm.ExitCell;
            int steps = Mathf.Max(Mathf.Abs(to.x - from.x), Mathf.Abs(to.y - from.y));
            for (int i = 0; i <= steps; i++)
            {
                Vector2 p = Vector2.Lerp(from, to, steps == 0 ? 0f : (float)i / steps);
                var pos = new Vector3Int(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y), 0);
                map.SetTile(pos, tile);
                map.SetColor(pos, PathColour);
            }
        }

        private static Transform[] BuildPlots(Transform parent, Grid grid, FarmDefinition farm, Sprite plotSprite, int openAtStart)
        {
            Transform container = Child(parent, "Plots");
            var plots = new Transform[farm.PlotCells.Count];
            for (int i = 0; i < plots.Length; i++)
            {
                var root = new GameObject("Plot_" + (i + 1).ToString("00"));
                root.transform.SetParent(container, false);
                Vector2Int cell = farm.PlotCells[i];
                root.transform.position = grid.CellToWorld(new Vector3Int(cell.x, cell.y, 0));
                // The plot and the tree planted on it sort as one object, by the plot's bottom corner.
                root.AddComponent<SortingGroup>();

                var soil = new GameObject("Soil");
                soil.transform.SetParent(root.transform, false);
                var sr = soil.AddComponent<SpriteRenderer>();
                sr.sprite = plotSprite;
                sr.color = i < openAtStart ? Color.white : LockedPlotTint;
                plots[i] = root.transform;
            }
            return plots;
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

        private static Transform Marker(Transform parent, Grid grid, Vector2Int cell, string name, Sprite tile, Color colour, string label)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = grid.GetCellCenterWorld(new Vector3Int(cell.x, cell.y, 0));

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
