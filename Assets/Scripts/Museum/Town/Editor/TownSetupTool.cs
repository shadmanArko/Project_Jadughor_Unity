using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectMuseum.Town.EditorTools
{
    /// <summary>
    /// One-shot generators for the town map and the right-side button canvas.
    ///
    /// Each command builds a prefab under Assets/Prefabs/Town/ and drops an instance into
    /// the open scene. After that the prefab is yours: edit it in the Scene view or prefab
    /// mode. Re-running a command REPLACES that prefab (it asks first), so only re-run it
    /// if you want to throw your edits away.
    ///
    /// Town prefab layout:
    ///   Town (TownMapController)             far from the museum, at <see cref="TownWorldPosition"/>
    ///   ├ World                              real sprites on the "Town" layer
    ///   │  ├ Ground, buildings, trees        positions / draw order / click outlines from Godot
    ///   │  ├ DiggingBuddyIndicator
    ///   │  └ Town Camera                     renders World into TownMap.renderTexture
    ///   └ Town Map Canvas                    overlay UI: backdrop, map RawImage, header, X, popup
    /// </summary>
    public static class TownSetupTool
    {
        private const string MenuRoot = "Tools/Project Museum/Town/";

        private const string LayoutPath = "Assets/2D/Museum/Town/town_layout.json";
        private const string SpriteFolder = "Assets/2D/Museum/Town/Sprites/";
        private const string UiFolder = "Assets/2D/UI/Museum Ui/Town/";
        private const string TownButtonIcon = "Assets/2D/UI/Museum Ui/Gameplay ui/Town_map_button.png";
        private const string DiggingButtonIcon = "Assets/2D/UI/Museum Ui/Gameplay ui/Digging_and_permits_button.png";
        private const string FontPath = "Assets/Fonts/PIXEAB__ SDF.asset";
        private const string UnlitMaterialPath = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";

        private const string PrefabFolder = "Assets/Prefabs/Town";
        private const string TownPrefabPath = PrefabFolder + "/Town.prefab";
        private const string RightCanvasPrefabPath = PrefabFolder + "/Right Side Canvas.prefab";
        private const string RenderTexturePath = PrefabFolder + "/TownMap.renderTexture";

        private const string TownLayerName = "Town";

        /// <summary>Far from the museum so no gameplay camera ever sees it.</summary>
        private static readonly Vector3 TownWorldPosition = new Vector3(500f, 500f, 0f);

        private const float PixelsPerUnit = 100f;  // import setting of the town sprites
        private const int UiPixelScale = 6;        // Godot 4x @720p = 6x @1080p
        private const int SortBand = 1000;         // sortingOrder = GodotZ * SortBand + y-rank
        private const int IndicatorOrder = 9000;

        private static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);

        // ── Menu ────────────────────────────────────────────────────────

        [MenuItem(MenuRoot + "Build Town (world + map UI)")]
        public static void BuildTown()
        {
            TownLayout layout = LoadLayout();
            if (layout == null) return;
            if (!ConfirmReplace(TownPrefabPath, FindInScene<TownMapController>())) return;

            EnsureFolder(PrefabFolder);
            int townLayer = EnsureLayer(TownLayerName);
            RenderTexture rt = EnsureRenderTexture(layout);

            var root = new GameObject("Town");
            root.transform.position = TownWorldPosition;
            TownMapController controller = root.AddComponent<TownMapController>();

            BuildWorld(root.transform, layout, townLayer, rt, out Camera townCamera,
                       out SpriteRenderer ground, out Transform indicator);
            BuildMapUi(root.transform, controller, townCamera, ground, indicator, rt);

            FinishPrefab(root, TownPrefabPath, FindInScene<TownMapController>());
        }

        [MenuItem(MenuRoot + "Build Right Side Canvas")]
        public static void BuildRightSideCanvas()
        {
            if (!ConfirmReplace(RightCanvasPrefabPath, FindInScene<RightSideButtonsController>())) return;
            EnsureFolder(PrefabFolder);

            GameObject root = CreateCanvas("Right Side Canvas", null, 10, 0.5f);
            RightSideButtonsController controller = root.AddComponent<RightSideButtonsController>();

            RectTransform column = CreateRect("Buttons", root.transform);
            column.anchorMin = column.anchorMax = column.pivot = Vector2.one;
            column.anchoredPosition = new Vector2(-22f, -110f);
            VerticalLayoutGroup vlg = column.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 16f;
            vlg.childAlignment = TextAnchor.UpperRight;
            vlg.childControlWidth = vlg.childControlHeight = false;
            vlg.childForceExpandWidth = vlg.childForceExpandHeight = false;
            ContentSizeFitter fitter = column.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            Button town = CreateIconButton("Town Map Button", column, LoadSprite(TownButtonIcon), 99f);
            UnityEventTools.AddPersistentListener(town.onClick, controller.OpenTownMap);

            Button digging = CreateIconButton("Digging Permits Button", column, LoadSprite(DiggingButtonIcon), 99f);
            UnityEventTools.AddPersistentListener(digging.onClick, controller.OpenDiggingPermits);

            SetField(controller, "notImplementedButtons", new UnityEngine.Object[] { digging });

            FinishPrefab(root, RightCanvasPrefabPath, FindInScene<RightSideButtonsController>());
        }

        [MenuItem(MenuRoot + "Re-sort Town by Y")]
        public static void ResortTown()
        {
            TownMapController town = Selection.activeGameObject != null
                ? Selection.activeGameObject.GetComponentInParent<TownMapController>()
                : null;
            if (town == null) town = FindInScene<TownMapController>().FirstOrDefault();
            if (town == null)
            {
                EditorUtility.DisplayDialog("Re-sort Town", "No Town in the open scene (or prefab stage).", "OK");
                return;
            }

            Transform world = town.transform.Find("World");
            List<SpriteRenderer> renderers = world.GetComponentsInChildren<SpriteRenderer>(true)
                .Where(r => r.name != "Ground" && r.sortingOrder < IndicatorOrder)
                .ToList();

            Undo.RecordObjects(renderers.ToArray<UnityEngine.Object>(), "Re-sort Town by Y");
            // Keep each sprite's Godot z band, then back-to-front by world y (higher y = further back).
            var sorted = renderers
                .OrderBy(r => r.sortingOrder / SortBand)
                .ThenByDescending(r => r.transform.position.y)
                .ToList();
            var rankInBand = new Dictionary<int, int>();
            foreach (SpriteRenderer r in sorted)
            {
                int band = r.sortingOrder / SortBand;
                rankInBand.TryGetValue(band, out int rank);
                rankInBand[band] = ++rank;
                r.sortingOrder = band * SortBand + rank;
                EditorUtility.SetDirty(r);
            }
            Debug.Log($"[TownSetupTool] Re-sorted {sorted.Count} town sprites by Y.");
        }

        // ── Town world ──────────────────────────────────────────────────

        private static void BuildWorld(Transform root, TownLayout layout, int layer, RenderTexture rt,
            out Camera townCamera, out SpriteRenderer ground, out Transform indicator)
        {
            Material unlit = AssetDatabase.LoadAssetAtPath<Material>(UnlitMaterialPath);

            var world = new GameObject("World").transform;
            world.SetParent(root, false);

            Sprite groundSprite = LoadSprite(SpriteFolder + layout.groundSprite + ".png");
            ground = CreateSprite("Ground", world, groundSprite, Vector2.zero, 0, unlit);

            // Godot draw order: z_index, then y-sort (lower y behind), then scene-tree order.
            var objects = layout.objects
                .OrderBy(o => o.z).ThenBy(o => o.y).ThenBy(o => o.order)
                .ToList();

            var groups = new Dictionary<string, Transform>();
            var rankInBand = new Dictionary<int, int>();
            TownObjectData buddy = null;
            SpriteRenderer buddyRenderer = null;

            foreach (TownObjectData o in objects)
            {
                Sprite sprite = LoadSprite(SpriteFolder + o.sprite + ".png");
                if (sprite == null) continue;

                // Keep Godot's grouping ("trees 1/tree4") so the hierarchy is easy to browse.
                Transform parent = world;
                string objectName = o.name;
                int slash = o.name.LastIndexOf('/');
                if (slash >= 0)
                {
                    string groupName = o.name.Substring(0, slash);
                    objectName = o.name.Substring(slash + 1);
                    if (!groups.TryGetValue(groupName, out parent))
                    {
                        parent = new GameObject(groupName).transform;
                        parent.SetParent(world, false);
                        groups[groupName] = parent;
                    }
                }

                rankInBand.TryGetValue(o.z, out int rank);
                rankInBand[o.z] = ++rank;

                Vector2 local = new Vector2(o.x, -o.y) / PixelsPerUnit;
                SpriteRenderer sr = CreateSprite(objectName, parent, sprite, local, o.z * SortBand + rank, unlit);

                if (o.kind != "decor" && o.polygon != null && o.polygon.Length >= 6)
                {
                    PolygonCollider2D collider = sr.gameObject.AddComponent<PolygonCollider2D>();
                    var points = new Vector2[o.polygon.Length / 2];
                    for (int i = 0; i < points.Length; i++)
                        points[i] = new Vector2(o.polygon[i * 2], -o.polygon[i * 2 + 1]) / PixelsPerUnit;
                    collider.points = points;
                    collider.isTrigger = true;

                    TownBuilding building = sr.gameObject.AddComponent<TownBuilding>();
                    building.EditorSetup(o.kind == "tree" ? TownObjectKind.Tree : TownObjectKind.Building,
                                         o.livingHouse, o.hasDiggingBuddy);
                }

                if (o.hasDiggingBuddy && buddy == null)
                {
                    buddy = o;
                    buddyRenderer = sr;
                }
            }

            indicator = null;
            if (buddy != null)
            {
                Sprite pointer = LoadSprite(UiFolder + "town_map_pointer.png");
                const float scale = 0.5f;
                float x = buddy.x - (buddyRenderer.sprite.rect.width + pointer.rect.width * scale) * 0.5f - 1f;
                SpriteRenderer ind = CreateSprite("DiggingBuddyIndicator", world, pointer,
                    new Vector2(x, -buddy.y) / PixelsPerUnit, IndicatorOrder, unlit);
                ind.transform.localScale = new Vector3(scale, scale, 1f);
                indicator = ind.transform;
            }

            // Camera framing the ground; TownMapView pans / zooms it at runtime.
            var camGo = new GameObject("Town Camera");
            camGo.transform.SetParent(world, false);
            camGo.transform.localPosition = new Vector3(0f, 0f, -10f);
            townCamera = camGo.AddComponent<Camera>();
            townCamera.orthographic = true;
            townCamera.orthographicSize = groundSprite.rect.height * 0.5f / PixelsPerUnit;
            townCamera.clearFlags = CameraClearFlags.SolidColor;
            townCamera.backgroundColor = new Color(0f, 0f, 0f, 0f); // ground's rounded corners stay see-through
            townCamera.cullingMask = 1 << layer;
            townCamera.targetTexture = rt;
            townCamera.depth = -10f;

            SetLayerRecursively(world.gameObject, layer);
        }

        // ── Town map UI ─────────────────────────────────────────────────

        private static void BuildMapUi(Transform root, TownMapController controller, Camera townCamera,
            SpriteRenderer ground, Transform indicator, RenderTexture rt)
        {
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

            // Match height: the map is 900 tall + header, so it always fits a 1080-unit-high canvas
            // (any aspect from 4:3 to ultrawide) at an exact 6x pixel scale.
            GameObject canvas = CreateCanvas("Town Map Canvas", root, 50, 1f);

            RectTransform panel = CreateRect("Map Panel", canvas.transform);
            Stretch(panel);

            // Backdrop: dims the museum and swallows clicks (Godot: translucent Panel).
            RectTransform backdrop = CreateRect("Backdrop", panel);
            Stretch(backdrop);
            backdrop.gameObject.AddComponent<Image>().color = new Color(0.1f, 0.1f, 0.1f, 0.35f);

            // The map. Its red border is part of the ground art, so there is no extra frame.
            Vector2 mapSize = ground.sprite.rect.size * UiPixelScale;
            RectTransform map = CreateRect("Map", panel);
            map.sizeDelta = mapSize;
            map.anchoredPosition = new Vector2(0f, -12f);
            RawImage raw = map.gameObject.AddComponent<RawImage>();
            raw.texture = rt;
            TownMapView view = map.gameObject.AddComponent<TownMapView>();
            SetField(view, "townCamera", townCamera);
            SetField(view, "ground", ground);

            float mapTop = map.anchoredPosition.y + mapSize.y * 0.5f;

            // Header bar just above the map, as in Godot (header.png, 66x13).
            Sprite headerSprite = LoadSprite(UiFolder + "town_header.png");
            RectTransform header = CreateRect("Header", panel);
            header.sizeDelta = new Vector2(305f, 60f);
            header.anchoredPosition = new Vector2(0f, mapTop - 5f + 30f);
            Image headerImage = header.gameObject.AddComponent<Image>();
            headerImage.sprite = headerSprite;
            headerImage.raycastTarget = false;
            TMP_Text title = CreateText("Title", header, font, "TOWN", 36f, new Color32(127, 32, 30, 255));
            title.alignment = TextAlignmentOptions.Center;

            // Close button, top-right above the map (Godot: themed "X" button).
            Sprite closeSprite = LoadSprite(UiFolder + "town_close_button.png");
            Button close = CreateIconButton("Close Button", panel, closeSprite, 66f);
            ((RectTransform)close.transform).anchoredPosition = new Vector2(mapSize.x * 0.5f - 39f, mapTop + 34f);
            UnityEventTools.AddPersistentListener(close.onClick, controller.RequestClose);

            // Popup (Godot TownWarning: grey box, light text, top-left).
            RectTransform popup = CreateRect("Popup", panel);
            popup.pivot = new Vector2(0f, 1f);
            popup.sizeDelta = new Vector2(600f, 120f);
            popup.anchoredPosition = new Vector2(-mapSize.x * 0.5f + 34f, mapTop + 63f);
            Image popupBg = popup.gameObject.AddComponent<Image>();
            popupBg.color = new Color32(72, 72, 72, 255);
            popupBg.raycastTarget = false;
            TMP_Text popupText = CreateText("Message", popup, font, "Looks like no one is home.", 26f,
                                            new Color32(198, 198, 198, 255));
            popupText.alignment = TextAlignmentOptions.Left;
            RectTransform textRect = popupText.rectTransform;
            textRect.offsetMin = new Vector2(16f, 0f);

            SetField(controller, "mapPanel", panel.gameObject);
            SetField(controller, "mapView", view);
            SetField(controller, "popup", popup.gameObject);
            SetField(controller, "popupText", popupText);
            SetField(controller, "diggingBuddyIndicator", indicator);
        }

        // ── Assets ──────────────────────────────────────────────────────

        private static TownLayout LoadLayout()
        {
            var json = AssetDatabase.LoadAssetAtPath<TextAsset>(LayoutPath);
            if (json == null)
            {
                EditorUtility.DisplayDialog("Build Town", $"Layout not found:\n{LayoutPath}", "OK");
                return null;
            }
            return JsonUtility.FromJson<TownLayout>(json.text);
        }

        private static RenderTexture EnsureRenderTexture(TownLayout layout)
        {
            Sprite ground = LoadSprite(SpriteFolder + layout.groundSprite + ".png");
            int w = (int)ground.rect.width * UiPixelScale;
            int h = (int)ground.rect.height * UiPixelScale;

            var rt = AssetDatabase.LoadAssetAtPath<RenderTexture>(RenderTexturePath);
            if (rt != null && rt.width == w && rt.height == h) return rt;
            if (rt != null) AssetDatabase.DeleteAsset(RenderTexturePath);

            rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32)
            {
                name = "TownMap",
                filterMode = FilterMode.Point,
                antiAliasing = 1,
                useMipMap = false
            };
            AssetDatabase.CreateAsset(rt, RenderTexturePath);
            return rt;
        }

        private static Sprite LoadSprite(string path)
        {
            Sprite sprite = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();
            if (sprite == null) Debug.LogError($"[TownSetupTool] No sprite at {path}");
            return sprite;
        }

        private static int EnsureLayer(string name)
        {
            int existing = LayerMask.NameToLayer(name);
            if (existing >= 0) return existing;

            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");
            for (int i = 8; i < layers.arraySize; i++)
            {
                SerializedProperty slot = layers.GetArrayElementAtIndex(i);
                if (!string.IsNullOrEmpty(slot.stringValue)) continue;
                slot.stringValue = name;
                tagManager.ApplyModifiedProperties();
                Debug.Log($"[TownSetupTool] Added layer '{name}' at index {i}.");
                return i;
            }

            Debug.LogWarning("[TownSetupTool] No free layer slot; the town uses Default (it's far enough away).");
            return 0;
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }

        // ── Prefab handling ─────────────────────────────────────────────

        private static List<T> FindInScene<T>() where T : Component =>
            UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None).ToList();

        private static bool ConfirmReplace<T>(string prefabPath, List<T> sceneInstances) where T : Component
        {
            bool prefabExists = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null;
            if (!prefabExists && sceneInstances.Count == 0) return true;

            string what = prefabExists ? $"{prefabPath} already exists" : "The scene already has one";
            return EditorUtility.DisplayDialog("Replace?",
                $"{what}.\n\nRebuilding replaces the prefab and the copy in the open scene. " +
                "Any edits you made to them will be lost.", "Replace", "Cancel");
        }

        private static void FinishPrefab<T>(GameObject root, string path, List<T> oldInstances) where T : Component
        {
            foreach (T old in oldInstances)
                if (old != null && old.gameObject != root)
                    Undo.DestroyObjectImmediate(PrefabUtility.GetOutermostPrefabInstanceRoot(old.gameObject) ?? old.gameObject);

            GameObject instance = PrefabUtility.SaveAsPrefabAssetAndConnect(root, path, InteractionMode.UserAction);
            Undo.RegisterCreatedObjectUndo(instance, $"Build {instance.name}");
            EditorSceneManager.MarkSceneDirty(instance.scene);
            Selection.activeGameObject = instance;
            Debug.Log($"[TownSetupTool] Built {path} and placed it in the scene. Save the scene to keep it.");
        }

        // ── Small builders ──────────────────────────────────────────────

        private static SpriteRenderer CreateSprite(string name, Transform parent, Sprite sprite, Vector2 localPos,
            int order, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            if (material != null) sr.sharedMaterial = material;
            return sr;
        }

        private static GameObject CreateCanvas(string name, Transform parent, int sortingOrder, float matchWidthOrHeight)
        {
            var go = new GameObject(name, typeof(RectTransform));
            if (parent != null) go.transform.SetParent(parent, false);
            go.layer = LayerMask.NameToLayer("UI");

            Canvas canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            CanvasScaler scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.matchWidthOrHeight = matchWidthOrHeight;

            go.AddComponent<GraphicRaycaster>();
            return go;
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            return rect;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static Button CreateIconButton(string name, Transform parent, Sprite icon, float size)
        {
            RectTransform rect = CreateRect(name, parent);
            rect.sizeDelta = new Vector2(size, size);
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = icon;
            image.preserveAspect = true;

            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(0.9f, 0.9f, 0.9f, 1f);
            colors.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
            colors.disabledColor = new Color(1f, 1f, 1f, 0.4f);
            button.colors = colors;
            return button;
        }

        private static TMP_Text CreateText(string name, Transform parent, TMP_FontAsset font, string text,
            float size, Color color)
        {
            RectTransform rect = CreateRect(name, parent);
            Stretch(rect);
            TextMeshProUGUI label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) label.font = font;
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.verticalAlignment = VerticalAlignmentOptions.Middle;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.raycastTarget = false;
            return label;
        }

        private static void SetLayerRecursively(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform) SetLayerRecursively(child.gameObject, layer);
        }

        /// <summary>Assigns a private [SerializeField] so the reference shows (and saves) in the inspector.</summary>
        private static void SetField(UnityEngine.Object target, string field, object value)
        {
            var so = new SerializedObject(target);
            SerializedProperty prop = so.FindProperty(field);
            if (prop == null) throw new ArgumentException($"{target.GetType().Name} has no serialized field '{field}'");

            if (value is UnityEngine.Object[] array)
            {
                prop.arraySize = array.Length;
                for (int i = 0; i < array.Length; i++)
                    prop.GetArrayElementAtIndex(i).objectReferenceValue = array[i];
            }
            else
            {
                prop.objectReferenceValue = value as UnityEngine.Object;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ── Layout data (written by GodotImport~/parse_town.py) ─────────

        [Serializable]
        private class TownLayout
        {
            public string groundSprite;
            public List<TownObjectData> objects = new();
        }

        [Serializable]
        private class TownObjectData
        {
            public string name;
            public string sprite;
            public float x, y;   // ground-pixel space, Godot convention (+y down)
            public int z;        // absolute Godot z_index
            public int order;    // scene-tree order
            public string kind;  // building / tree / decor
            public bool livingHouse;
            public bool hasDiggingBuddy;
            public float[] polygon;
        }
    }
}
