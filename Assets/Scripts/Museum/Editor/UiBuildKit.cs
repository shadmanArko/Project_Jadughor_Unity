using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectMuseum.EditorTools
{
    /// <summary>
    /// Shared helpers for the one-shot UI prefab generators (HUD, map selection). They build
    /// plain uGUI hierarchies with pixel-art friendly defaults; the results are ordinary
    /// prefabs meant to be hand-edited afterwards.
    /// </summary>
    internal static class UiBuildKit
    {
        public const string FontPath = "Assets/Fonts/PIXEAB__ SDF.asset";
        public static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);

        // ── Assets ──────────────────────────────────────────────────────

        public static Sprite LoadSprite(string path)
        {
            Sprite sprite = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();
            if (sprite == null) Debug.LogError($"[UiBuildKit] No sprite at {path}");
            return sprite;
        }

        public static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }

        /// <summary>Adds the scenes to Build Settings (enabled) if missing; keeps the existing order.</summary>
        public static void EnsureScenesInBuild(params string[] scenePaths)
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            bool changed = false;
            foreach (string path in scenePaths)
            {
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
                {
                    Debug.LogWarning($"[UiBuildKit] Scene not found: {path}");
                    continue;
                }
                int i = scenes.FindIndex(s => s.path == path);
                if (i >= 0)
                {
                    if (scenes[i].enabled) continue;
                    scenes[i] = new EditorBuildSettingsScene(path, true);
                }
                else
                {
                    scenes.Add(new EditorBuildSettingsScene(path, true));
                }
                changed = true;
                Debug.Log($"[UiBuildKit] Added {path} to Build Settings.");
            }
            if (changed) EditorBuildSettings.scenes = scenes.ToArray();
        }

        /// <summary>
        /// A copy of the font's material with an outline, saved next to the font so prefabs can
        /// reference it (setting outline on a label directly makes an unsaved material instance).
        /// </summary>
        public static Material EnsureOutlineMaterial(TMP_FontAsset font, string suffix, Color outline, float width)
        {
            if (font == null) return null;
            string dir = Path.GetDirectoryName(AssetDatabase.GetAssetPath(font))?.Replace('\\', '/');
            string path = $"{dir}/{font.name} - {suffix}.mat";

            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(font.material) { name = $"{font.name} - {suffix}" };
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.EnableKeyword(ShaderUtilities.Keyword_Outline);
            mat.SetColor(ShaderUtilities.ID_OutlineColor, outline);
            mat.SetFloat(ShaderUtilities.ID_OutlineWidth, width);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ── Prefab handling ─────────────────────────────────────────────

        public static List<T> FindInScene<T>() where T : Component =>
            UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None).ToList();

        public static bool ConfirmReplace<T>(string prefabPath, List<T> sceneInstances) where T : Component
        {
            bool prefabExists = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null;
            if (!prefabExists && sceneInstances.Count == 0) return true;

            string what = prefabExists ? $"{prefabPath} already exists" : "The scene already has one";
            return EditorUtility.DisplayDialog("Replace?",
                $"{what}.\n\nRebuilding replaces the prefab and the copy in the open scene. " +
                "Any edits you made to them will be lost.", "Replace", "Cancel");
        }

        public static GameObject FinishPrefab<T>(GameObject root, string path, List<T> oldInstances) where T : Component
        {
            foreach (T old in oldInstances)
                if (old != null && old.gameObject != root)
                    Undo.DestroyObjectImmediate(PrefabUtility.GetOutermostPrefabInstanceRoot(old.gameObject) ?? old.gameObject);

            GameObject instance = PrefabUtility.SaveAsPrefabAssetAndConnect(root, path, InteractionMode.UserAction);
            Undo.RegisterCreatedObjectUndo(instance, $"Build {instance.name}");
            EditorSceneManager.MarkSceneDirty(instance.scene);
            Selection.activeGameObject = instance;
            Debug.Log($"[UiBuildKit] Built {path} and placed it in the scene. Save the scene to keep it.");
            return instance;
        }

        /// <summary>Assigns a private [SerializeField] (object, object array or string) via SerializedObject.</summary>
        public static void SetField(UnityEngine.Object target, string field, object value)
        {
            var so = new SerializedObject(target);
            SerializedProperty prop = so.FindProperty(field);
            if (prop == null) throw new ArgumentException($"{target.GetType().Name} has no serialized field '{field}'");

            switch (value)
            {
                case UnityEngine.Object[] array:
                    prop.arraySize = array.Length;
                    for (int i = 0; i < array.Length; i++)
                        prop.GetArrayElementAtIndex(i).objectReferenceValue = array[i];
                    break;
                case string text:
                    prop.stringValue = text;
                    break;
                default:
                    prop.objectReferenceValue = value as UnityEngine.Object;
                    break;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ── Builders ────────────────────────────────────────────────────

        public static GameObject CreateCanvas(string name, int sortingOrder, float matchWidthOrHeight)
        {
            var go = new GameObject(name, typeof(RectTransform));
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

        public static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            return rect;
        }

        /// <summary>Top-left anchored placement in the parent, in pixels with y pointing down.</summary>
        public static RectTransform Place(RectTransform rect, float x, float y, float w, float h)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(w, h);
            return rect;
        }

        public static RectTransform Stretch(RectTransform rect, float left = 0, float top = 0, float right = 0, float bottom = 0)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
            return rect;
        }

        public static Image AddImage(RectTransform rect, Sprite sprite, bool raycast = false)
        {
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = raycast;
            return image;
        }

        public static Image AddColor(RectTransform rect, Color color, bool raycast = false)
        {
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = raycast;
            return image;
        }

        /// <summary>9-sliced pixel-art image whose border pixels render <paramref name="pixelScale"/> units wide.</summary>
        public static Image AddSliced(RectTransform rect, Sprite sprite, float pixelScale, bool raycast = false)
        {
            Image image = AddImage(rect, sprite, raycast);
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 1f / pixelScale;
            return image;
        }

        public static Button AddButton(GameObject go, Graphic target)
        {
            if (target != null) target.raycastTarget = true;
            Button button = go.AddComponent<Button>();
            button.targetGraphic = target;
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(0.9f, 0.9f, 0.9f, 1f);
            colors.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(1f, 1f, 1f, 0.45f);
            button.colors = colors;
            return button;
        }

        public static TMP_Text CreateText(string name, Transform parent, TMP_FontAsset font, Material material,
            string text, float size, Color color, TextAlignmentOptions alignment = TextAlignmentOptions.Center)
        {
            RectTransform rect = CreateRect(name, parent);
            Stretch(rect);
            TextMeshProUGUI label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) label.font = font;
            if (material != null) label.fontSharedMaterial = material;
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.alignment = alignment;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.raycastTarget = false;
            return label;
        }
    }
}
