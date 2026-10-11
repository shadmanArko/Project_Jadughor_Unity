using System.Collections.Generic;
using ProjectMuseum.Clock;
using ProjectMuseum.MapSelection;
using ProjectMuseum.Town;
using Systems.MineSystem.Mine.Enum;
using Systems.MineSystem.Mine.Service.MineArtifactService.Config;
using Systems.MineSystem.MinePlayerSystem.Scriptable;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;
using static ProjectMuseum.EditorTools.UiBuildKit;

namespace ProjectMuseum.EditorTools
{
    /// <summary>
    /// One-shot generators for the museum HUD pieces ported from Godot:
    /// - Build Time UI → Assets/Prefabs/HUD/Time Canvas.prefab (clock, calendar, pause/speed,
    ///   OPEN/CLOSE gate sign) — Godot "Top Panel/Date time panel".
    /// - Build Map Selection Panel → Assets/Prefabs/HUD/Map Selection Canvas.prefab — Godot
    ///   "DiggingAndPermitsMenu"; also creates MapSelectionData, adds Museum + Mine to Build
    ///   Settings and enables the Digging button on the Right Side Canvas prefab.
    ///
    /// After building, the prefabs are yours to edit. Re-running a command replaces that
    /// prefab and its scene copy (it asks first).
    /// </summary>
    public static class MuseumHudSetupTool
    {
        private const string MenuRoot = "Tools/Project Museum/HUD/";

        private const string PrefabFolder = "Assets/Prefabs/HUD";
        private const string TimePrefabPath = PrefabFolder + "/Time Canvas.prefab";
        private const string MapPrefabPath = PrefabFolder + "/Map Selection Canvas.prefab";

        private const string DateTimeArt = "Assets/2D/UI/Museum Ui/Date Time/";
        private const string GateSignArt = "Assets/2D/UI/Museum Ui/Gate Sign/";
        private const string CloseButtonSprite = "Assets/2D/UI/Museum Ui/Town/town_close_button.png";

        private const string MapDataPath = "Assets/GameData/MapSelectionData.asset";
        private const string MinePlayerPath = "Assets/Systems/MineSystem/MinePlayerSystem/Scriptable/MinePlayerScriptable.asset";
        private const string ArtifactConfigPath = "Assets/Systems/MineSystem/Mine/Service/MineArtifactService/Config/ArtifactGenerationConfig.asset";
        private const string RightCanvasPrefabPath = "Assets/Prefabs/Town/Right Side Canvas.prefab";
        private const string MuseumScenePath = "Assets/Scenes/Museum.unity";
        private const string MineScenePath = "Assets/Scenes/Mine.unity";

        // Godot theme "Museum ui purple Bold": text #3D193F, outline #FEF3E2.
        private static readonly Color Purple = new Color32(0x3D, 0x19, 0x3F, 0xFF);
        private static readonly Color Cream = new Color32(0xFE, 0xF3, 0xE2, 0xFF);

        private const int Px = 6; // UI pixel scale at 1080p (Godot ~4.5x at 720p)

        // ── Time UI ─────────────────────────────────────────────────────

        [MenuItem(MenuRoot + "Build Time UI")]
        public static void BuildTimeUi()
        {
            if (!ConfirmReplace(TimePrefabPath, FindInScene<MuseumTimeSystem>())) return;
            EnsureFolder(PrefabFolder);

            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            Material outlined = EnsureOutlineMaterial(font, "Cream Outline", Cream, 0.25f);

            GameObject root = CreateCanvas("Time Canvas", 10, 0.5f);
            root.AddComponent<MuseumTimeSystem>();

            // Panel hugs the top-right corner, left of the right-side button column.
            RectTransform panel = CreateRect("Date Time Panel", root.transform);
            panel.anchorMin = panel.anchorMax = panel.pivot = Vector2.one;
            panel.anchoredPosition = new Vector2(-140f, -8f);
            panel.sizeDelta = new Vector2(808f, 122f);
            MuseumTimeUI ui = panel.gameObject.AddComponent<MuseumTimeUI>();

            // Gate sign (56x40 @3x) — click flips OPEN / CLOSE.
            Sprite[] closing = LoadFrames("gate_closing_");
            Sprite[] opening = LoadFrames("gate_opening_");
            RectTransform signRect = Place(CreateRect("Gate Sign", panel), 0, 0, 56 * 3, 40 * 3);
            Image signImage = AddImage(signRect, closing[0], raycast: true);
            MuseumGateSign sign = signRect.gameObject.AddComponent<MuseumGateSign>();
            SetField(sign, "closingFrames", closing);
            SetField(sign, "openingFrames", opening);
            Button signButton = AddButton(signRect.gameObject, signImage);
            UnityEventTools.AddPersistentListener(signButton.onClick, sign.Toggle);

            const float bookX = 174f;
            AddArt("Book", panel, "Journal_Time_Holder", bookX, 0);

            // Date strip sits behind the clock and runs off to its right (Godot z -1).
            const float clockX = bookX + 108f;
            const float stripX = clockX + 162f - 44f;
            RectTransform strip = AddArt("Date Strip", panel, "Base_Time_Holder", stripX, 0);

            float hx = 51f;
            TMP_Text dayName = AddHolder(strip, "Day Name", "date holder 1", ref hx, "Mo", font, outlined);
            TMP_Text day = AddHolder(strip, "Day", "date holder 2", ref hx, "01", font, outlined);
            RectTransform seasonRect = AddArt("Season", strip, "spring", hx, 0); hx += seasonRect.sizeDelta.x + 6f;
            TMP_Text year = AddHolder(strip, "Year", "date holder 4", ref hx, "Yr 01", font, outlined);

            RectTransform clock = AddArt("Clock", panel, "Watchl_Time_Holder", clockX, 0);
            TMP_Text time = CreateText("Time", clock, font, outlined, "09:50", 26f, Purple);
            Stretch(time.rectTransform, 24f, 16f, 36f, 15f);

            // Pause + speed buttons under the strip (Godot: "//", 1x, 2x, 4x).
            Sprite buttonSprite = LoadSprite(DateTimeArt + "Speed_Time_Holder.png");
            float bx = clockX + 162f - 8f;
            const float by = 120f - 52f;
            Image pause = AddTimeButton(panel, "Pause Play", "//", buttonSprite, ref bx, by, font, outlined,
                                        b => UnityEventTools.AddPersistentListener(b.onClick, ui.OnPausePlayPressed));
            var speedImages = new List<Object>();
            foreach (int speed in new[] { 1, 2, 4 })
            {
                int s = speed;
                speedImages.Add(AddTimeButton(panel, $"Speed {s}x", $"{s}x", buttonSprite, ref bx, by, font, outlined,
                    b => UnityEventTools.AddIntPersistentListener(b.onClick, ui.OnSpeedPressed, s)));
            }

            SetField(ui, "clockText", time);
            SetField(ui, "dayNameText", dayName);
            SetField(ui, "dayText", day);
            SetField(ui, "yearText", year);
            SetField(ui, "seasonIcon", seasonRect.GetComponent<Image>());
            SetField(ui, "seasonSprites", new Object[]
            {
                LoadSprite(DateTimeArt + "spring.png"), LoadSprite(DateTimeArt + "summer.png"),
                LoadSprite(DateTimeArt + "autum.png"), LoadSprite(DateTimeArt + "winter.png")
            });
            SetField(ui, "pauseButtonImage", pause);
            SetField(ui, "speedButtonImages", speedImages.ToArray());

            FinishPrefab(root, TimePrefabPath, FindInScene<MuseumTimeSystem>());
        }

        private static Sprite[] LoadFrames(string prefix)
        {
            var frames = new Sprite[7];
            for (int i = 0; i < frames.Length; i++) frames[i] = LoadSprite($"{GateSignArt}{prefix}{i}.png");
            return frames;
        }

        /// <summary>A Date Time sprite at native size × <see cref="Px"/>, top-left at (x, y).</summary>
        private static RectTransform AddArt(string name, Transform parent, string sprite, float x, float y)
        {
            Sprite s = LoadSprite(DateTimeArt + sprite + ".png");
            RectTransform rect = Place(CreateRect(name, parent), x, y, s.rect.width * Px, s.rect.height * Px);
            AddImage(rect, s);
            return rect;
        }

        private static TMP_Text AddHolder(RectTransform strip, string name, string sprite, ref float x,
            string text, TMP_FontAsset font, Material material)
        {
            RectTransform holder = AddArt(name, strip, sprite, x, 0);
            x += holder.sizeDelta.x + 6f;
            TMP_Text label = CreateText("Label", holder, font, material, text, 24f, Purple);
            Stretch(label.rectTransform, 0, 8f, 0, 0); // Godot offset_top 8
            return label;
        }

        private static Image AddTimeButton(RectTransform panel, string name, string text, Sprite sprite,
            ref float x, float y, TMP_FontAsset font, Material material, System.Action<Button> wire)
        {
            RectTransform rect = Place(CreateRect(name, panel), x, y, 75f, 48f);
            x += 83f;
            Image image = AddSliced(rect, sprite, Px, raycast: true);
            Button button = AddButton(rect.gameObject, image);
            wire(button);
            CreateText("Label", rect, font, material, text, 24f, Purple);
            return image;
        }

        // ── Map selection ───────────────────────────────────────────────

        [MenuItem(MenuRoot + "Build Map Selection Panel")]
        public static void BuildMapSelectionPanel()
        {
            if (!ConfirmReplace(MapPrefabPath, FindInScene<MapSelectionPanelController>())) return;
            EnsureFolder(PrefabFolder);

            MapSelectionData data = EnsureMapData();
            EnsureScenesInBuild(MuseumScenePath, MineScenePath);
            EnableDiggingButton();

            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            Color window = Cream;
            Color selector = new Color32(0xE9, 0xCE, 0xAA, 0xFF);
            Color list = new Color32(0xCD, 0xAA, 0x82, 0xFF);

            // Match height: the 960-tall window always fits.
            GameObject root = CreateCanvas("Map Selection Canvas", 40, 1f);
            MapSelectionPanelController controller = root.AddComponent<MapSelectionPanelController>();

            RectTransform panel = Stretch(CreateRect("Panel", root.transform));
            AddColor(Stretch(CreateRect("Backdrop", panel)), new Color(0f, 0f, 0f, 0.45f), raycast: true);

            // Godot: 1080x640 PanelContainer @720p → 1620x960 @1080p.
            RectTransform win = CreateRect("Window", panel);
            win.sizeDelta = new Vector2(1620f, 960f);
            AddColor(win, window, raycast: true);

            RectTransform close = CreateRect("Close Button", win);
            close.anchorMin = close.anchorMax = Vector2.one;
            close.sizeDelta = new Vector2(60f, 60f);
            close.anchoredPosition = new Vector2(-38f, -38f);
            Button closeButton = AddButton(close.gameObject, AddImage(close, LoadSprite(CloseButtonSprite)));
            UnityEventTools.AddPersistentListener(closeButton.onClick, controller.Close);

            RectTransform selectorRect = Place(CreateRect("Location Selector Panel", win), 60f, 75f, 1500f, 855f);
            AddColor(selectorRect, selector);

            // Region row: < NORTH AMERICA >
            Button prev = RegionArrow(selectorRect, "Previous Region", "<", 30f, font);
            UnityEventTools.AddPersistentListener(prev.onClick, controller.PreviousRegion);
            Button next = RegionArrow(selectorRect, "Next Region", ">", 1500f - 30f - 195f, font);
            UnityEventTools.AddPersistentListener(next.onClick, controller.NextRegion);
            RectTransform labelRect = Place(CreateRect("Region Label", selectorRect), 225f, 15f, 1050f, 90f);
            TMP_Text regionLabel = CreateText("Text", labelRect, font, null, "NORTH AMERICA", 48f, Purple);

            // Site list (Godot: vertical scroll always shown).
            RectTransform listBg = Place(CreateRect("Sublocation Panel", selectorRect), 30f, 135f, 1440f, 690f);
            AddColor(listBg, list);
            BuildScrollList(listBg, font, out RectTransform content, out Button template);

            RectTransform empty = Stretch(CreateRect("Empty Message", listBg));
            CreateText("Text", empty, font, null, "No dig sites here yet.", 36f, Purple);
            empty.gameObject.SetActive(false);

            RectTransform loading = Stretch(CreateRect("Loading Overlay", panel));
            AddColor(loading, new Color(0f, 0f, 0f, 0.8f), raycast: true);
            CreateText("Text", loading, font, null, "Loading...", 64f, Cream);

            SetField(controller, "panelRoot", panel.gameObject);
            SetField(controller, "regionLabel", regionLabel);
            SetField(controller, "previousRegionButton", prev);
            SetField(controller, "nextRegionButton", next);
            SetField(controller, "siteListContent", content);
            SetField(controller, "siteButtonTemplate", template);
            SetField(controller, "emptyListMessage", empty.gameObject);
            SetField(controller, "loadingOverlay", loading.gameObject);
            SetField(controller, "data", data);
            SetField(controller, "minePlayer", AssetDatabase.LoadAssetAtPath<MinePlayerScriptable>(MinePlayerPath));
            SetField(controller, "mineArtifactConfig", AssetDatabase.LoadAssetAtPath<ArtifactGenerationConfig>(ArtifactConfigPath));
            SetField(controller, "mineSceneName", "Mine");

            // Left visible so it can be edited; the controller hides it on Awake.
            loading.gameObject.SetActive(false);
            FinishPrefab(root, MapPrefabPath, FindInScene<MapSelectionPanelController>());
        }

        private static Button RegionArrow(RectTransform parent, string name, string text, float x, TMP_FontAsset font)
        {
            RectTransform rect = Place(CreateRect(name, parent), x, 15f, 195f, 90f);
            Button button = AddButton(rect.gameObject, AddColor(rect, Cream));
            CreateText("Text", rect, font, null, text, 56f, Purple);
            return button;
        }

        private static void BuildScrollList(RectTransform area, TMP_FontAsset font,
            out RectTransform content, out Button template)
        {
            ScrollRect scroll = area.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;

            RectTransform viewport = Stretch(CreateRect("Viewport", area), 0, 0, 36f, 0);
            viewport.gameObject.AddComponent<RectMask2D>();
            AddColor(viewport, Color.clear, raycast: true);

            content = CreateRect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = Vector2.zero;
            VerticalLayoutGroup layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(24, 24, 24, 24);
            layout.spacing = 16f;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Template site button (Godot: min height 50 @720p → 75).
            RectTransform t = CreateRect("Site Button Template", content);
            t.sizeDelta = new Vector2(0f, 75f);
            template = AddButton(t.gameObject, AddColor(t, Cream));
            TMP_Text label = CreateText("Text", t, font, null, "Possible Battle Site", 34f, Purple,
                                        TextAlignmentOptions.Left);
            Stretch(label.rectTransform, 24f, 0, 24f, 0);

            // Vertical scrollbar, always visible.
            RectTransform bar = CreateRect("Scrollbar", area);
            bar.anchorMin = new Vector2(1f, 0f);
            bar.anchorMax = Vector2.one;
            bar.pivot = new Vector2(1f, 0.5f);
            bar.sizeDelta = new Vector2(28f, -16f);
            bar.anchoredPosition = new Vector2(-4f, 0f);
            AddColor(bar, new Color(0f, 0f, 0f, 0.2f), raycast: true);
            RectTransform sliding = Stretch(CreateRect("Sliding Area", bar), 2f, 2f, 2f, 2f);
            RectTransform handle = Stretch(CreateRect("Handle", sliding));
            Image handleImage = AddColor(handle, Cream, raycast: true);
            Scrollbar scrollbar = bar.gameObject.AddComponent<Scrollbar>();
            scrollbar.handleRect = handle;
            scrollbar.targetGraphic = handleImage;
            scrollbar.direction = Scrollbar.Direction.BottomToTop;

            scroll.viewport = viewport;
            scroll.content = content;
            scroll.verticalScrollbar = scrollbar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
        }

        /// <summary>Creates the site list asset with Godot's one entry, unless it already exists.</summary>
        private static MapSelectionData EnsureMapData()
        {
            var data = AssetDatabase.LoadAssetAtPath<MapSelectionData>(MapDataPath);
            if (data != null) return data;

            EnsureFolder("Assets/GameData");
            data = ScriptableObject.CreateInstance<MapSelectionData>();
            data.regions.Add(new MapRegion
            {
                displayName = "NORTH AMERICA",
                region = Region.America,
                sites = { new MapSite { displayName = "Possible Battle Site", site = Site.BattleSite, available = true } }
            });
            AssetDatabase.CreateAsset(data, MapDataPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[MuseumHudSetupTool] Created {MapDataPath}.");
            return data;
        }

        /// <summary>Takes the Digging button off the Right Side Canvas's "not implemented" list.</summary>
        private static void EnableDiggingButton()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(RightCanvasPrefabPath) == null) return;

            GameObject contents = PrefabUtility.LoadPrefabContents(RightCanvasPrefabPath);
            try
            {
                var controller = contents.GetComponentInChildren<RightSideButtonsController>(true);
                if (controller == null) return;
                SetField(controller, "notImplementedButtons", new Object[0]);
                PrefabUtility.SaveAsPrefabAsset(contents, RightCanvasPrefabPath);
                Debug.Log("[MuseumHudSetupTool] Enabled the Digging & Permits button on the Right Side Canvas.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }
    }
}
