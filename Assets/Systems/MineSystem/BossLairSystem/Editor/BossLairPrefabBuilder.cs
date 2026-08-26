using System.IO;
using Systems.MineSystem.BossLairSystem.Config;
using Systems.MineSystem.BossLairSystem.View;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;

namespace Systems.MineSystem.BossLairSystem.Editor
{
    /// <summary>
    /// Builds a boss lair arena prefab with the component layout gameplay code
    /// depends on.
    /// </summary>
    /// <remarks>
    /// The walls and backdrop are painted at runtime by
    /// <c>BossLairShellGenerationService</c>, so this only creates the empty
    /// structure: the grid, the three tilemaps, the collider trio on the correct
    /// layer, the camera bounds, the light and the anchors. Getting that collider
    /// setup wrong by hand is the usual cause of "the player falls through the
    /// floor", which is why it is scripted.
    /// </remarks>
    public static class BossLairPrefabBuilder
    {
        private const string PrefabDirectory =
            "Assets/Systems/MineSystem/BossLairSystem/Prefab";
        private const string PrefabPath = PrefabDirectory + "/BossLairView.prefab";
        private const string MineViewPrefabPath =
            "Assets/Systems/MineSystem/Mine/Prefab/MineView.prefab";
        private const string WallLayerName = "Wall";
        private const float FallbackCellSize = 0.2f;
        private const int FallbackWidth = 15;
        private const int FallbackHeight = 8;

        [MenuItem("Tools/Boss Lair/Create Lair Prefab")]
        public static void CreateLairPrefab()
        {
            var wallLayer = LayerMask.NameToLayer(WallLayerName);
            if (wallLayer < 0)
            {
                Debug.LogError(
                    $"[BossLair] No '{WallLayerName}' layer exists. The player's " +
                    "ground probe queries that layer, so the arena floor would " +
                    "not be solid.");
                return;
            }

            var cellSize = ResolveMineCellSize();
            ResolveArenaSize(out var width, out var height);

            var root = new GameObject("BossLairView");
            var view = root.AddComponent<BossLairView>();

            var gridObject = new GameObject("LairGrid");
            gridObject.transform.SetParent(root.transform, false);
            var grid = gridObject.AddComponent<Grid>();
            grid.cellSize = new Vector3(cellSize, cellSize, 0f);

            view.grid = grid;
            view.backgroundTileMap = CreateTilemap(gridObject, "Background", 0);
            view.decorTileMap = CreateTilemap(gridObject, "Decor", 1);
            view.wallTileMap = CreateSolidTilemap(gridObject, "Wall", 2, wallLayer);

            view.cameraBoundaryCollider =
                CreateCameraBounds(root, width, height, cellSize);
            view.arenaLight = CreateArenaLight(root, width, height, cellSize);

            // Anchors sit on the interior floor row. Local cell (0,0) is the
            // bottom-left interior cell, and the generated shell fills y = -1.
            view.exitAnchor = CreateAnchor(root, grid, "ExitAnchor", 0, 0);
            view.playerSpawnPoint =
                CreateAnchor(root, grid, "PlayerSpawn", Mathf.Min(2, width - 1), 0);
            view.bossSpawnPoint = CreateAnchor(
                root, grid, "BossSpawn", Mathf.Max(0, width - 3), 0);

            Directory.CreateDirectory(PrefabDirectory);
            var savedPath = AssetDatabase.GenerateUniqueAssetPath(PrefabPath);
            PrefabUtility.SaveAsPrefabAsset(root, savedPath);
            Object.DestroyImmediate(root);
            AssetDatabase.Refresh();

            Debug.Log(
                $"[BossLair] Created {savedPath} for a {width}x{height} arena " +
                $"(cell size {cellSize}). Walls and backdrop are generated at " +
                "runtime; paint decoration into the Decor tilemap and assign the " +
                "prefab to the BossLairInstaller.");
            Selection.activeObject =
                AssetDatabase.LoadAssetAtPath<GameObject>(savedPath);
        }

        private static Tilemap CreateTilemap(
            GameObject gridObject,
            string name,
            int sortingOrder)
        {
            var tilemapObject = new GameObject(name);
            tilemapObject.transform.SetParent(gridObject.transform, false);
            var tilemap = tilemapObject.AddComponent<Tilemap>();
            var renderer = tilemapObject.AddComponent<TilemapRenderer>();
            renderer.sortingOrder = sortingOrder;
            return tilemap;
        }

        /// <summary>
        /// Wall tilemap with the collider pair the player's physics queries need:
        /// a static body, a composite collider, and a tilemap collider feeding it.
        /// Mirrors how MineView authors its own wall tilemap.
        /// </summary>
        private static Tilemap CreateSolidTilemap(
            GameObject gridObject,
            string name,
            int sortingOrder,
            int wallLayer)
        {
            var tilemap = CreateTilemap(gridObject, name, sortingOrder);
            var target = tilemap.gameObject;
            target.layer = wallLayer;

            var body = target.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Static;

            var composite = target.AddComponent<CompositeCollider2D>();
            composite.geometryType = CompositeCollider2D.GeometryType.Outlines;

            var tilemapCollider = target.AddComponent<TilemapCollider2D>();
            tilemapCollider.usedByComposite = true;
            return tilemap;
        }

        private static BoxCollider2D CreateCameraBounds(
            GameObject root,
            int width,
            int height,
            float cellSize)
        {
            var boundsObject = new GameObject("CameraBounds");
            boundsObject.transform.SetParent(root.transform, false);
            var collider = boundsObject.AddComponent<BoxCollider2D>();
            // A trigger so the confiner shape never participates in collisions.
            collider.isTrigger = true;
            collider.size = new Vector2(width * cellSize, height * cellSize);
            collider.offset = collider.size * 0.5f;
            return collider;
        }

        /// <summary>
        /// Point light rather than global: URP 2D accumulates global lights on a
        /// shared blend style, so a global light here would also brighten the
        /// mine, which is still being lit by MineDarkeningService.
        /// </summary>
        private static Light2D CreateArenaLight(
            GameObject root,
            int width,
            int height,
            float cellSize)
        {
            var lightObject = new GameObject("ArenaLight");
            lightObject.transform.SetParent(root.transform, false);
            lightObject.transform.localPosition = new Vector3(
                width * cellSize * 0.5f, height * cellSize * 0.5f, 0f);

            var light = lightObject.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Point;
            light.pointLightOuterRadius =
                Mathf.Max(width, height) * cellSize * 0.75f;
            light.intensity = 1f;
            return light;
        }

        private static Transform CreateAnchor(
            GameObject root,
            Grid grid,
            string name,
            int cellX,
            int cellY)
        {
            var anchor = new GameObject(name);
            anchor.transform.SetParent(root.transform, false);
            anchor.transform.localPosition =
                grid.GetCellCenterLocal(new Vector3Int(cellX, cellY, 0));
            return anchor.transform;
        }

        private static float ResolveMineCellSize()
        {
            var mineView = AssetDatabase.LoadAssetAtPath<GameObject>(
                MineViewPrefabPath);
            var grid = mineView != null
                ? mineView.GetComponentInChildren<Grid>()
                : null;
            if (grid == null || grid.cellSize.x <= 0f)
            {
                Debug.LogWarning(
                    "[BossLair] Could not read the mine grid cell size; using " +
                    $"{FallbackCellSize}. The lair cell size must match the mine, " +
                    "because weapon reach and fall damage derive from it.");
                return FallbackCellSize;
            }
            return grid.cellSize.x;
        }

        private static void ResolveArenaSize(out int width, out int height)
        {
            width = FallbackWidth;
            height = FallbackHeight;
            var guids = AssetDatabase.FindAssets(
                $"t:{nameof(BossProceduralLairConfig)}");
            if (guids.Length == 0)
                return;
            var config = AssetDatabase.LoadAssetAtPath<BossProceduralLairConfig>(
                AssetDatabase.GUIDToAssetPath(guids[0]));
            if (config == null)
                return;
            width = config.InteriorWidthInCells;
            height = config.InteriorHeightInCells;
        }
    }
}
