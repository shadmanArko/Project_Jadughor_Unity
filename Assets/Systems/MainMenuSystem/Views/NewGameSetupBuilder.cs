using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Systems.MainMenuSystem.Views
{
    /// <summary>
    /// Builds the New Game setup screen 1:1 from Godot's <c>NewGameSetupUi.tscn</c>.
    ///
    /// Every rect below is the Godot anchor/offset pair copied verbatim (Godot's
    /// 1280x720 design space, y-down) and converted with <see cref="Place"/> into
    /// this canvas's 1920x1080 reference space (x1.5). Font sizes scale the same way
    /// (Godot default 16px → 24). Colours are Godot's default theme plus the two
    /// "Museum ui" theme colours used by the loading bar.
    ///
    /// Runs once when the main menu scene starts — no per-frame cost.
    /// </summary>
    public static class NewGameSetupBuilder
    {
        private const float K = 1920f / 1280f; // Godot → Unity reference scale

        // Godot default theme (Panel/LineEdit/Button stylebox ≈ 0.1 grey @ 60% over 0.3 clear colour)
        private static readonly Color PanelBg = new(0.18f, 0.18f, 0.18f, 1f);
        private static readonly Color ControlBg = new(0.13f, 0.13f, 0.13f, 1f);
        private static readonly Color ControlHover = new(0.22f, 0.22f, 0.22f, 1f);
        private static readonly Color ControlPressed = new(0.09f, 0.09f, 0.09f, 1f);
        private static readonly Color FontColor = new(0.875f, 0.875f, 0.875f, 1f);
        private static readonly Color PlaceholderColor = new(0.875f, 0.875f, 0.875f, 0.6f);

        // "Museum ui" theme colours (loading panel)
        private static readonly Color LoadingBg = new(0.212f, 0.212f, 0.212f, 1f);
        private static readonly Color Cream = new(0.996f, 0.953f, 0.886f, 1f);
        private static readonly Color Orange = new(0.867f, 0.478f, 0.235f, 1f);
        private static readonly Color Plum = new(0.239f, 0.098f, 0.247f, 1f);

        public static readonly string[] GenderOptions = { "Male", "Female", "Others" };

        private static Sprite _circle, _triangle;

        public static NewGameSetupView Build(Sprite characterArt, bool tutorialToggleInteractable)
        {
            // Own overlay canvas so the main menu canvas can be hidden independently
            // (Godot: MainMenu Ui.Visible = false, NewGameSetupUi.Visible = true).
            var root = new GameObject("NewGameSetupCanvas", typeof(RectTransform));
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            root.AddComponent<GraphicRaycaster>();
            var view = root.AddComponent<NewGameSetupView>();

            // ── Panel ────────────────────────────────────────────────────
            var panel = NewImage("Panel", root.transform, PanelBg);
            Place(panel.rectTransform, 0, 0, 1, 1, 0, 0, 0, 0);

            // Character art: Sprite2D at (970,274), scale 0.225 of 3301x2034.
            if (characterArt != null)
            {
                var art = NewImage("MaleAndFemaleMainj", panel.transform, Color.white);
                art.sprite = characterArt;
                art.preserveAspect = true;
                art.raycastTarget = false;
                float w = characterArt.rect.width * 0.225f, h = characterArt.rect.height * 0.225f;
                Place(art.rectTransform, 0, 0, 0, 0, 970 - w / 2, 274 - h / 2, 970 + w / 2, 274 + h / 2);
            }

            // Name:  [LineEdit]
            var nameLabel = NewLabel("Player name", panel.transform, "Name:", 16);
            Place(nameLabel.rectTransform, 0, 0, 0, 0, 32, 92, 92, 118);
            var nameInput = NewInputField(panel.transform, "Enter Your Name...");
            Place((RectTransform)nameInput.transform, 0, 0, 0, 0, 32 + 76, 92 - 2, 32 + 238, 92 + 29);

            // Gender: [OptionButton Male/Female/Others]
            var genderLabel = NewLabel("Gender", panel.transform, "Gender:", 16);
            Place(genderLabel.rectTransform, 0, 0, 0, 0, 32, 140, 137, 166);
            var gender = NewDropdown(panel.transform, GenderOptions);
            const float genderMid = (140 + 166) / 2f;
            Place((RectTransform)gender.transform, 0, 0, 0, 0, 32 + 76, genderMid - 13.5f, 32 + 175, genderMid + 17.5f);

            // Show Tutorial [CheckButton] — bottom-left
            var tutLabel = NewLabel("Turorial", panel.transform, "Show Tutorial", 16);
            Place(tutLabel.rectTransform, 0, 1, 0, 1, 32, -46, 139, -20);
            var tutorial = NewSwitch(panel.transform);
            Place((RectTransform)tutorial.transform, 0, 1, 0, 1, 32 + 132, -46, 32 + 176, -46 + 24);
            tutorial.isOn = true; // Godot: button_pressed = true
            tutorial.interactable = tutorialToggleInteractable; // Godot shipped it disabled = true

            // [Start..] — bottom-right
            var start = NewButton("Start Button", panel.transform, "Start..", 16);
            Place((RectTransform)start.transform, 1, 1, 1, 1, -156, -46, -32, -15);

            // ── Warning UI ("Please Enter Your Name.") ───────────────────
            var warning = NewImage("Warning UI", panel.transform, PanelBg);
            Place(warning.rectTransform, 0.312f, 0.386f, 0.683f, 0.645f, 0, 0, 0, 0);
            var warnText = NewLabel("RichTextLabel", warning.transform, "Please Enter Your Name.", 32);
            warnText.color = Color.white;
            warnText.alignment = TextAlignmentOptions.Center;
            Place(warnText.rectTransform, 0, 0, 1, 1, 0, 0, 0, -66);
            var okay = NewButton("Okay Button", warning.transform, "Okay", 32);
            Place((RectTransform)okay.transform, 0.5f, 1, 0.5f, 1, -72, -66, 63, -14);
            warning.gameObject.SetActive(false);

            // ── Loading Panel ────────────────────────────────────────────
            var loading = NewImage("Loading Panel", root.transform, LoadingBg);
            Place(loading.rectTransform, 0, 0, 1, 1, 0, 0, 0, 0);
            var loadingText = NewLabel("Loading", loading.transform, "Loading...", 35);
            loadingText.color = Cream;
            loadingText.fontStyle = FontStyles.Bold;
            loadingText.outlineColor = Plum;
            loadingText.outlineWidth = 0.25f;
            loadingText.textWrappingMode = TextWrappingModes.NoWrap;
            Place(loadingText.rectTransform, 0, 1, 1, 1, 127, -200, -132, -177);

            var barBg = NewImage("ProgressBar", loading.transform, Orange);
            Place(barBg.rectTransform, 0, 1, 1, 1, 127, -148, -130, -91);
            var barBorder = NewImage("FillBorder", barBg.transform, Orange); // 5px orange border
            Stretch(barBorder.rectTransform);
            var fill = NewImage("Fill", barBorder.transform, Cream);
            Stretch(fill.rectTransform, 5 * K);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillAmount = 0;
            fill.sprite = WhiteSprite(); // Filled type needs a sprite
            var percent = NewLabel("Percent", barBg.transform, "0%", 32);
            percent.color = Cream;
            percent.outlineColor = Plum;
            percent.outlineWidth = 0.25f;
            percent.alignment = TextAlignmentOptions.Center;
            Stretch(percent.rectTransform);
            loading.gameObject.SetActive(false);

            view.Bind(panel.gameObject, nameInput, gender, tutorial, start, warning.gameObject, okay,
                loading.gameObject, loadingText, fill, percent);
            return view;
        }

        // ── Layout ───────────────────────────────────────────────────────

        /// <summary>
        /// Apply a Godot Control layout. Anchors are 0..1 from top-left (y down);
        /// offsets are Godot pixels (1280x720) and are scaled by <see cref="K"/>.
        /// </summary>
        private static void Place(RectTransform rt, float aLeft, float aTop, float aRight, float aBottom,
            float oLeft, float oTop, float oRight, float oBottom)
        {
            rt.anchorMin = new Vector2(aLeft, 1 - aBottom);
            rt.anchorMax = new Vector2(aRight, 1 - aTop);
            rt.offsetMin = new Vector2(oLeft * K, -oBottom * K);
            rt.offsetMax = new Vector2(oRight * K, -oTop * K);
        }

        private static void Stretch(RectTransform rt, float inset = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
        }

        // ── Widgets ──────────────────────────────────────────────────────

        private static Image NewImage(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = color;
            return img;
        }

        private static TextMeshProUGUI NewLabel(string name, Transform parent, string text, float godotSize)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = godotSize * K;
            t.color = FontColor;
            t.alignment = TextAlignmentOptions.MidlineLeft;
            t.raycastTarget = false;
            return t;
        }

        /// <summary>
        /// Selectable tints multiply the target graphic, so the graphic stays white and
        /// the ColorBlock carries Godot's normal/hover/pressed stylebox colours directly.
        /// </summary>
        private static void ApplyColors(Selectable s, Color normal)
        {
            s.targetGraphic.color = Color.white;
            var c = s.colors;
            c.normalColor = normal;
            c.highlightedColor = ControlHover;
            c.selectedColor = ControlHover;
            c.pressedColor = ControlPressed;
            c.disabledColor = new Color(normal.r, normal.g, normal.b, 0.5f);
            c.colorMultiplier = 1;
            s.colors = c;
        }

        private static Button NewButton(string name, Transform parent, string text, float godotSize)
        {
            var bg = NewImage(name, parent, ControlBg);
            var b = bg.gameObject.AddComponent<Button>();
            b.targetGraphic = bg;
            ApplyColors(b, ControlBg);
            var label = NewLabel("Text", bg.transform, text, godotSize);
            label.alignment = TextAlignmentOptions.Center;
            Stretch(label.rectTransform);
            return b;
        }

        private static TMP_InputField NewInputField(Transform parent, string placeholder)
        {
            var bg = NewImage("LineEdit", parent, ControlBg);
            var field = bg.gameObject.AddComponent<TMP_InputField>();
            field.targetGraphic = bg;
            ApplyColors(field, ControlBg);

            var area = new GameObject("Text Area", typeof(RectTransform), typeof(RectMask2D));
            area.transform.SetParent(bg.transform, false);
            var areaRt = (RectTransform)area.transform;
            Stretch(areaRt, 6 * K);

            var ph = NewLabel("Placeholder", area.transform, placeholder, 16);
            ph.color = PlaceholderColor;
            ph.textWrappingMode = TextWrappingModes.NoWrap;
            Stretch(ph.rectTransform);
            var txt = NewLabel("Text", area.transform, "", 16);
            txt.textWrappingMode = TextWrappingModes.NoWrap;
            Stretch(txt.rectTransform);

            field.textViewport = areaRt;
            field.textComponent = txt;
            field.placeholder = ph;
            field.pointSize = 16 * K;
            field.characterLimit = 24;
            field.lineType = TMP_InputField.LineType.SingleLine;
            field.caretColor = FontColor;
            field.customCaretColor = true;
            return field;
        }

        private static TMP_Dropdown NewDropdown(Transform parent, string[] options)
        {
            var bg = NewImage("OptionButton", parent, ControlBg);
            var dd = bg.gameObject.AddComponent<TMP_Dropdown>();
            dd.targetGraphic = bg;
            ApplyColors(dd, ControlBg);

            var caption = NewLabel("Label", bg.transform, options[0], 16);
            Stretch(caption.rectTransform);
            caption.rectTransform.offsetMin = new Vector2(6 * K, 0);
            caption.rectTransform.offsetMax = new Vector2(-22 * K, 0);

            var arrow = NewImage("Arrow", bg.transform, FontColor);
            arrow.sprite = Triangle();
            arrow.raycastTarget = false;
            var art = arrow.rectTransform;
            art.anchorMin = art.anchorMax = new Vector2(1, 0.5f);
            art.sizeDelta = new Vector2(9 * K, 6 * K);
            art.anchoredPosition = new Vector2(-11 * K, 0);

            // Template (popup) — hidden; TMP_Dropdown clones it when opened.
            float itemH = 26 * K;
            var template = NewImage("Template", bg.transform, PanelBg);
            var trt = template.rectTransform;
            trt.anchorMin = new Vector2(0, 0);
            trt.anchorMax = new Vector2(1, 0);
            trt.pivot = new Vector2(0.5f, 1);
            trt.anchoredPosition = new Vector2(0, -2);
            trt.sizeDelta = new Vector2(0, itemH * options.Length + 8);
            template.gameObject.AddComponent<ScrollRect>();
            var scroll = template.GetComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;

            var viewport = NewImage("Viewport", template.transform, Color.white);
            viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            Stretch(viewport.rectTransform, 4);
            var content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(viewport.transform, false);
            var crt = (RectTransform)content.transform;
            crt.anchorMin = new Vector2(0, 1);
            crt.anchorMax = new Vector2(1, 1);
            crt.pivot = new Vector2(0.5f, 1);
            crt.sizeDelta = new Vector2(0, itemH);
            scroll.content = crt;
            scroll.viewport = viewport.rectTransform;

            var item = new GameObject("Item", typeof(RectTransform), typeof(Toggle));
            item.transform.SetParent(content.transform, false);
            var irt = (RectTransform)item.transform;
            irt.anchorMin = new Vector2(0, 0.5f);
            irt.anchorMax = new Vector2(1, 0.5f);
            irt.sizeDelta = new Vector2(0, itemH);
            var itemBg = NewImage("Item Background", item.transform, PanelBg);
            Stretch(itemBg.rectTransform);
            var check = NewImage("Item Checkmark", item.transform, FontColor);
            check.sprite = Circle();
            var ckrt = check.rectTransform;
            ckrt.anchorMin = ckrt.anchorMax = new Vector2(0, 0.5f);
            ckrt.sizeDelta = new Vector2(6 * K, 6 * K);
            ckrt.anchoredPosition = new Vector2(8 * K, 0);
            var itemLabel = NewLabel("Item Label", item.transform, "Option", 16);
            Stretch(itemLabel.rectTransform);
            itemLabel.rectTransform.offsetMin = new Vector2(18 * K, 0);

            var toggle = item.GetComponent<Toggle>();
            toggle.targetGraphic = itemBg;
            toggle.graphic = check;
            toggle.isOn = true;
            ApplyColors(toggle, PanelBg);

            dd.template = trt;
            dd.captionText = caption;
            dd.itemText = itemLabel;
            dd.options.Clear();
            foreach (var o in options) dd.options.Add(new TMP_Dropdown.OptionData(o));
            dd.value = 0; // Godot: selected = 0 (Male)
            dd.RefreshShownValue();
            template.gameObject.SetActive(false);
            return dd;
        }

        /// <summary>Godot CheckButton look: rounded-ish track + knob that slides.</summary>
        private static Toggle NewSwitch(Transform parent)
        {
            var track = NewImage("CheckButton", parent, ControlBg);
            var toggle = track.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = track;
            ApplyColors(toggle, ControlBg);
            // "On" fill sits over the track (tinted by the toggle's ColorBlock) and
            // under the knob; Godot 4 default accent colour.
            var onFill = NewImage("OnFill", track.transform, new Color(0.44f, 0.73f, 0.98f, 1f));
            onFill.raycastTarget = false;
            Stretch(onFill.rectTransform, 2 * K);
            var knob = NewImage("Knob", track.transform, FontColor);
            knob.sprite = Circle();
            knob.raycastTarget = false;
            var krt = knob.rectTransform;
            krt.anchorMin = krt.anchorMax = new Vector2(0, 0.5f);
            krt.sizeDelta = new Vector2(18 * K, 18 * K);
            var visual = track.gameObject.AddComponent<SwitchToggleVisual>();
            visual.Init(toggle, onFill, krt);
            return toggle;
        }

        // ── Procedural sprites (placeholder-grade, created once) ─────────

        private static Sprite WhiteSprite() =>
            Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));

        private static Sprite Circle()
        {
            if (_circle) return _circle;
            const int s = 32;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            var px = new Color32[s * s];
            float r = s / 2f;
            for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                px[y * s + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(r - d) * 255));
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return _circle = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f));
        }

        private static Sprite Triangle()
        {
            if (_triangle) return _triangle;
            const int w = 32, h = 20;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                // point down: full width at top row, zero at bottom row
                float half = (w / 2f) * (y + 0.5f) / h;
                for (int x = 0; x < w; x++)
                {
                    float a = Mathf.Clamp01(half - Mathf.Abs(x + 0.5f - w / 2f) + 0.5f);
                    px[y * w + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return _triangle = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f));
        }
    }
}
