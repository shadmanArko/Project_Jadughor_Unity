using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectMuseum.Builder.EditorTools
{
    /// <summary>
    /// One-click generator for the exhibit editor UI. Builds the canvas + panel
    /// hierarchy (dimmer bg, ~70% window, left scroll list, right centered grid,
    /// bottom bar, close button, drag layer) into the OPEN scene, generates the
    /// ArtifactCard + ArtifactSlot prefabs, and wires every <see cref="ExhibitEditorUI"/>
    /// serialized field. Everything is real Unity objects, so all sprite/font/script
    /// references resolve correctly (unlike hand-authored YAML).
    ///
    /// Run <c>Tools ▸ Project Museum ▸ Build Exhibit Editor UI</c>, then tweak
    /// colours/sizes to taste. Re-running replaces the two prefabs and adds a fresh
    /// canvas (delete the old "Exhibit Editor Canvas" first if regenerating).
    /// </summary>
    public static class ExhibitEditorUIBuilder
    {
        private const string PrefabFolder = "Assets/Prefabs/Exhibit Editor";
        private const string CardPrefabPath = PrefabFolder + "/ArtifactCard.prefab";
        private const string SlotPrefabPath = PrefabFolder + "/ArtifactSlot.prefab";

        private static readonly Color DimColor = new Color(0f, 0f, 0f, 0.55f);
        private static readonly Color WindowColor = new Color(0.12f, 0.10f, 0.09f, 0.98f);
        private static readonly Color PanelColor = new Color(1f, 1f, 1f, 0.05f);
        private static readonly Color SlotColor = new Color(1f, 1f, 1f, 0.10f);
        private static readonly Color CardColor = new Color(1f, 1f, 1f, 0.08f);
        private static readonly Color Accent = new Color(0.85f, 0.45f, 0.25f, 1f);

        [MenuItem("Tools/Project Museum/Build Exhibit Editor UI")]
        public static void Build()
        {
            EnsureFolder(PrefabFolder);

            GameObject cardPrefab = BuildCardPrefab();
            GameObject slotPrefab = BuildSlotPrefab();

            EnsureEventSystem();

            // ── Canvas ──────────────────────────────────────────────────
            var canvasGo = new GameObject("Exhibit Editor Canvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100; // above the gameplay/builder UI
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            // ── Root (always active) + Panel (toggled) ──────────────────
            RectTransform root = NewUI("ExhibitEditor", canvasGo.transform);
            Stretch(root);
            var editor = root.gameObject.AddComponent<ExhibitEditorUI>();

            RectTransform panel = NewUI("Panel", root);
            Stretch(panel);

            // Dimmer backdrop (blocks clicks behind, closes nothing by itself).
            RectTransform dim = NewUI("Dimmer", panel);
            Stretch(dim);
            AddImage(dim, DimColor);

            // Window — the ~70% box, centred.
            RectTransform window = NewUI("Window", panel);
            Anchor(window, new Vector2(0.15f, 0.15f), new Vector2(0.85f, 0.85f));
            AddImage(window, WindowColor);

            // ── Left: scrollable artifact list ──────────────────────────
            RectTransform left = NewUI("Left Panel", window);
            Anchor(left, new Vector2(0.02f, 0.04f), new Vector2(0.40f, 0.96f));
            AddImage(left, PanelColor);
            var scroll = left.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;

            RectTransform viewport = NewUI("Viewport", left);
            Stretch(viewport);
            AddImage(viewport, new Color(0, 0, 0, 0.001f)); // near-invisible, receives scroll drags
            viewport.gameObject.AddComponent<RectMask2D>();

            RectTransform content = NewUI("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = Vector2.zero;
            content.offsetMax = Vector2.zero;
            var vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(8, 8, 8, 8);
            vlg.spacing = 8;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            var vFit = content.gameObject.AddComponent<ContentSizeFitter>();
            vFit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.viewport = viewport;
            scroll.content = content;

            // ── Right: centred slot grid + bottom bar ───────────────────
            RectTransform right = NewUI("Right Panel", window);
            Anchor(right, new Vector2(0.42f, 0.04f), new Vector2(0.98f, 0.96f));

            RectTransform gridArea = NewUI("Slot Grid Area", right);
            Anchor(gridArea, new Vector2(0f, 0.18f), new Vector2(1f, 1f)); // above the bottom bar

            RectTransform grid = NewUI("Grid Content", gridArea);
            grid.anchorMin = grid.anchorMax = grid.pivot = new Vector2(0.5f, 0.5f); // centred
            var glg = grid.gameObject.AddComponent<GridLayoutGroup>();
            glg.cellSize = new Vector2(88, 88);
            glg.spacing = Vector2.zero; // 0 spacing — always-on cell borders merge into grid lines
            glg.childAlignment = TextAnchor.MiddleCenter;
            glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            glg.constraintCount = 4;
            var gFit = grid.gameObject.AddComponent<ContentSizeFitter>();
            gFit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            gFit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            RectTransform bottomBar = NewUI("Bottom Bar (future buttons)", right);
            Anchor(bottomBar, new Vector2(0f, 0f), new Vector2(1f, 0.15f));
            AddImage(bottomBar, PanelColor);

            // Close button (top-right of the window).
            RectTransform close = NewUI("Close Button", window);
            close.anchorMin = close.anchorMax = new Vector2(1f, 1f);
            close.pivot = new Vector2(1f, 1f);
            close.anchoredPosition = new Vector2(-8f, -8f);
            close.sizeDelta = new Vector2(44f, 44f);
            var closeImg = AddImage(close, Accent);
            var closeBtn = close.gameObject.AddComponent<Button>();
            closeBtn.targetGraphic = closeImg;
            var closeX = AddText(close, "X", "X", 26, FontStyles.Bold, TextAlignmentOptions.Center);
            Stretch((RectTransform)closeX.transform);

            // Drag layer — last child so the drag ghost renders on top of everything.
            RectTransform dragLayer = NewUI("Drag Layer", panel);
            Stretch(dragLayer);

            // ── Wire the ExhibitEditorUI serialized fields ──────────────
            var so = new SerializedObject(editor);
            SetRef(so, "panelRoot", panel.gameObject);
            SetRef(so, "closeButton", closeBtn);
            SetRef(so, "storageContent", content);
            SetRef(so, "cardPrefab", cardPrefab.GetComponent<ArtifactCard>());
            SetRef(so, "slotGridContent", grid);
            SetRef(so, "slotGrid", glg);
            SetRef(so, "slotPrefab", slotPrefab.GetComponent<ArtifactSlot>());
            SetRef(so, "dragLayer", dragLayer);
            so.FindProperty("slotsPerTileAxis").intValue = 2; // 1×1 exhibit → 2×2 (4 cells)
            so.FindProperty("debugFillStorageFromCatalog").boolValue = true;
            SetColorIfPresent(so, "emptyColor", new Color(1f, 1f, 1f, 0.06f));
            SetColorIfPresent(so, "occupiedColor", new Color(0.85f, 0.55f, 0.25f, 0.35f));
            SetColorIfPresent(so, "availableColor", new Color(0.4f, 1f, 0.4f, 0.15f));
            SetColorIfPresent(so, "lineDefaultColor", new Color(1f, 1f, 1f, 0.25f));
            SetColorIfPresent(so, "availablePerimeterColor", Color.white);
            SetColorIfPresent(so, "availableInnerColor", new Color(1f, 1f, 1f, 0.4f));
            SetColorIfPresent(so, "unavailablePerimeterColor", Color.black);
            SetColorIfPresent(so, "unavailableInnerColor", new Color(0f, 0f, 0f, 0.4f));
            SetColorIfPresent(so, "ghostBackdropColor", new Color(1f, 1f, 1f, 0.25f));
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(canvasGo.scene);
            Selection.activeGameObject = root.gameObject;
            Debug.Log("[ExhibitEditorUIBuilder] Built 'Exhibit Editor Canvas' and wired ExhibitEditorUI. " +
                      "Card/slot prefabs are in " + PrefabFolder + ". Tweak colours/sizes to taste.");
        }

        // ── Prefabs ─────────────────────────────────────────────────────

        private static GameObject BuildCardPrefab()
        {
            RectTransform root = NewUI("ArtifactCard", null);
            root.sizeDelta = new Vector2(0, 120);
            AddImage(root, CardColor);
            var hlg = root.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(8, 8, 8, 8);
            hlg.spacing = 10;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            var le = root.gameObject.AddComponent<LayoutElement>();
            le.minHeight = 120;
            le.preferredHeight = 120;
            var card = root.gameObject.AddComponent<ArtifactCard>();

            RectTransform icon = NewUI("Icon", root);
            var iconImg = AddImage(icon, Color.white);
            iconImg.preserveAspect = true;
            var iconLe = icon.gameObject.AddComponent<LayoutElement>();
            iconLe.minWidth = 100; iconLe.preferredWidth = 100;
            iconLe.minHeight = 100; iconLe.preferredHeight = 100;

            RectTransform texts = NewUI("Texts", root);
            var tv = texts.gameObject.AddComponent<VerticalLayoutGroup>();
            tv.childControlWidth = true; tv.childControlHeight = true;
            tv.childForceExpandWidth = true; tv.childForceExpandHeight = false;
            tv.spacing = 2;
            var tle = texts.gameObject.AddComponent<LayoutElement>();
            tle.flexibleWidth = 1;

            var nameLabel = AddText(texts, "Name", "Artifact Name", 28, FontStyles.Bold, TextAlignmentOptions.Left);
            var tagsLabel = AddText(texts, "Tags", "Era  Region  Object", 18, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            tagsLabel.color = new Color(1f, 1f, 1f, 0.6f);

            var so = new SerializedObject(card);
            SetRef(so, "icon", iconImg);
            SetRef(so, "nameLabel", nameLabel);
            SetRef(so, "tagsLabel", tagsLabel);
            so.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root.gameObject, CardPrefabPath);
            Object.DestroyImmediate(root.gameObject);
            return prefab;
        }

        private static GameObject BuildSlotPrefab()
        {
            RectTransform root = NewUI("ArtifactSlot", null);
            root.sizeDelta = new Vector2(88, 88);
            var bgImg = AddImage(root, SlotColor); // background = highlight/tint layer

            RectTransform icon = NewUI("Icon", root);
            Anchor(icon, Vector2.zero, Vector2.one);
            icon.offsetMin = new Vector2(6, 6);
            icon.offsetMax = new Vector2(-6, -6);
            var iconImg = AddImage(icon, Color.white);
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false; // let the cell background receive drops/drags
            iconImg.enabled = false;       // empty slot by default

            // Four thin edge strips (disabled by default) used to outline placement groups.
            const float t = 3f;
            Image top = AddEdge(root, "BorderTop", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1f), new Vector2(0, t));
            Image bottom = AddEdge(root, "BorderBottom", new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0f), new Vector2(0, t));
            Image left = AddEdge(root, "BorderLeft", new Vector2(0, 0), new Vector2(0, 1), new Vector2(0f, 0.5f), new Vector2(t, 0));
            Image right = AddEdge(root, "BorderRight", new Vector2(1, 0), new Vector2(1, 1), new Vector2(1f, 0.5f), new Vector2(t, 0));

            var slot = root.gameObject.AddComponent<ArtifactSlot>();
            var so = new SerializedObject(slot);
            SetRef(so, "background", bgImg);
            SetRef(so, "icon", iconImg);
            SetRef(so, "borderTop", top);
            SetRef(so, "borderBottom", bottom);
            SetRef(so, "borderLeft", left);
            SetRef(so, "borderRight", right);
            so.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root.gameObject, SlotPrefabPath);
            Object.DestroyImmediate(root.gameObject);
            return prefab;
        }

        // ── Helpers ─────────────────────────────────────────────────────

        private static RectTransform NewUI(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            if (parent != null) go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static Image AddImage(RectTransform rt, Color color)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            return img;
        }

        /// <summary>A thin edge strip anchored to one side of the cell — disabled until a group outlines it.</summary>
        private static Image AddEdge(RectTransform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 sizeDelta)
        {
            RectTransform rt = NewUI(name, parent);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = sizeDelta;
            var img = rt.gameObject.AddComponent<Image>();
            img.color = new Color(1f, 1f, 1f, 0.25f); // default grid-line tint (recoloured at runtime)
            img.raycastTarget = false;
            img.enabled = true; // always visible — spacing 0 merges them into grid lines
            return img;
        }

        private static TextMeshProUGUI AddText(Transform parent, string name, string text,
            float size, FontStyles style, TextAlignmentOptions align)
        {
            RectTransform rt = NewUI(name, parent);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = size;
            t.fontStyle = style;
            t.alignment = align;
            t.color = Color.white;
            if (TMP_Settings.defaultFontAsset != null) t.font = TMP_Settings.defaultFontAsset;
            return t;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static void Anchor(RectTransform rt, Vector2 min, Vector2 max)
        {
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static void SetRef(SerializedObject so, string field, Object value)
        {
            SerializedProperty p = so.FindProperty(field);
            if (p != null) p.objectReferenceValue = value;
            else Debug.LogWarning($"[ExhibitEditorUIBuilder] No serialized field '{field}' on ExhibitEditorUI.");
        }

        private static void SetColorIfPresent(SerializedObject so, string field, Color value)
        {
            SerializedProperty p = so.FindProperty(field);
            if (p != null) p.colorValue = value;
        }

        private static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() != null) return;
            var es = new GameObject("EventSystem",
                typeof(UnityEngine.EventSystems.EventSystem),
                typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
            Debug.Log("[ExhibitEditorUIBuilder] Added an EventSystem (InputSystemUIInputModule).", es);
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = Path.GetDirectoryName(folder).Replace("\\", "/");
            string leaf = Path.GetFileName(folder);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
