using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ProjectMuseum.Characters.EditorTools
{
    /// <summary>
    /// Generates <see cref="AnimationClip"/> assets that key the integer <c>Frame</c> field of a
    /// <see cref="SheetSpriteRenderer"/> or <see cref="SheetSpriteGroup"/>.
    ///
    /// Hand-keying these in the Animation window means one key per frame per clip — the Godot
    /// scenes carried five to nine keys across a dozen clips per character, and the guest
    /// stacked that across eight layers. Describing a clip as "frames 0-7 at 10 fps, looping"
    /// and pressing a button is the same result without the clicking.
    ///
    /// Keys use constant (stepped) tangents on a discrete curve, matching Godot's
    /// <c>update = 1</c> so poses snap instead of blending through half-frames.
    ///
    /// Tools ▸ Project Museum ▸ Frame Animation Clip Builder
    /// </summary>
    public class FrameClipBuilderWindow : EditorWindow
    {
        [Serializable]
        class ClipDefinition
        {
            public string Name = "new_clip";

            /// <summary>Frame list: ranges and singles, e.g. "0-7", "24,25,26", "8-15,0".</summary>
            public string Frames = "0-7";

            public float FrameRate = 10f;
            public bool Loop = true;
        }

        [SerializeField] GameObject _animatorRoot;
        [SerializeField] GameObject _frameTarget;
        [SerializeField] AnimatorController _controller;
        [SerializeField] string _outputFolder = "Assets/Animations/Characters";
        [SerializeField] List<ClipDefinition> _clips = new List<ClipDefinition>();

        [SerializeField] string _pasteText = "";

        Vector2 _scroll;
        int _previewFrame;
        bool _pasteExpanded;
        string _selectedPresetNote;

        [MenuItem("Tools/Project Museum/Frame Animation Clip Builder")]
        static void Open()
        {
            var window = GetWindow<FrameClipBuilderWindow>("Frame Clips");
            window.minSize = new Vector2(420f, 480f);
        }

        void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            EditorGUILayout.LabelField("Target", EditorStyles.boldLabel);
            _animatorRoot = (GameObject)EditorGUILayout.ObjectField(
                new GUIContent("Animator Root", "The GameObject holding the Animator. Clip paths are relative to this."),
                _animatorRoot, typeof(GameObject), true);

            _frameTarget = (GameObject)EditorGUILayout.ObjectField(
                new GUIContent("Frame Target", "The GameObject with the SheetSpriteRenderer or SheetSpriteGroup to animate. " +
                                               "Leave empty to use the Animator Root."),
                _frameTarget, typeof(GameObject), true);

            var target = _frameTarget != null ? _frameTarget : _animatorRoot;
            var component = ResolveFrameComponent(target, out var componentType);

            if (_animatorRoot == null)
            {
                EditorGUILayout.HelpBox("Assign an Animator Root to begin.", MessageType.Info);
                EditorGUILayout.EndScrollView();
                return;
            }

            if (component == null)
            {
                EditorGUILayout.HelpBox(
                    "No SheetSpriteRenderer or SheetSpriteGroup found on the frame target. " +
                    "A SheetSpriteGroup is preferred for layered characters — one track drives every layer.",
                    MessageType.Warning);
            }

            var relativePath = AnimationUtility.CalculateTransformPath(target.transform, _animatorRoot.transform);
            EditorGUILayout.LabelField("Bound Path",
                string.IsNullOrEmpty(relativePath) ? "<root>" : relativePath);
            EditorGUILayout.LabelField("Bound Property",
                component == null ? "-" : $"{componentType.Name}.Frame");

            DrawSheetInfo(component);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                _outputFolder = EditorGUILayout.TextField("Folder", _outputFolder);
                if (GUILayout.Button("Browse", GUILayout.Width(70f)))
                {
                    var picked = EditorUtility.SaveFolderPanel("Clip output folder", "Assets", "");
                    if (!string.IsNullOrEmpty(picked)) _outputFolder = ToProjectRelative(picked);
                }
            }

            _controller = (AnimatorController)EditorGUILayout.ObjectField(
                new GUIContent("Animator Controller", "Optional. Generated clips are added as states, " +
                                                      "so you get a state per clip ready to wire transitions."),
                _controller, typeof(AnimatorController), false);

            EditorGUILayout.Space();
            DrawPresets(component);

            EditorGUILayout.Space();
            DrawPasteList();

            EditorGUILayout.Space();
            DrawClipList(component);

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(component == null || _clips.Count == 0))
            {
                if (GUILayout.Button("Generate Clips", GUILayout.Height(30f)))
                    Generate(relativePath, componentType);
            }

            EditorGUILayout.EndScrollView();
        }

        /// <summary>
        /// The renderer whose grid describes the target. For a group that is its first layer —
        /// every layer of a character shares one grid layout, so any of them will do.
        /// </summary>
        static SheetSpriteRenderer ResolveSampleRenderer(Component component)
        {
            if (component is SheetSpriteRenderer renderer) return renderer;

            if (component is SheetSpriteGroup group)
            {
                foreach (var layer in group.Layers)
                {
                    if (layer?.Renderer != null) return layer.Renderer;
                }
            }

            return null;
        }

        void DrawSheetInfo(Component component)
        {
            var renderer = ResolveSampleRenderer(component);
            if (renderer == null || renderer.Sheet == null) return;

            EditorGUILayout.LabelField("Sheet",
                $"{renderer.Sheet.name} — {renderer.Columns}x{renderer.Rows} ({renderer.FrameCount} frames)");

            // A picker here means you can look up a pose index while writing the frame ranges
            // instead of switching back to the character's inspector.
            _previewFrame = SheetGridGUI.DrawSheetGrid(renderer.Sheet, renderer.Columns, renderer.Rows, _previewFrame);
            EditorGUILayout.LabelField("Clicked Frame", _previewFrame.ToString());
        }

        void DrawClipList(Component component)
        {
            EditorGUILayout.LabelField("Clips", EditorStyles.boldLabel);

            for (var i = 0; i < _clips.Count; i++)
            {
                var clip = _clips[i];
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        clip.Name = EditorGUILayout.TextField(clip.Name);
                        if (GUILayout.Button("X", GUILayout.Width(24f)))
                        {
                            _clips.RemoveAt(i);
                            return;
                        }
                    }

                    clip.Frames = EditorGUILayout.TextField(
                        new GUIContent("Frames", "Ranges and singles: \"0-7\", \"24,25,26\", \"8-15,0\""),
                        clip.Frames);
                    clip.FrameRate = EditorGUILayout.FloatField("Frame Rate", clip.FrameRate);
                    clip.Loop = EditorGUILayout.Toggle("Loop", clip.Loop);

                    var parsed = ParseFrames(clip.Frames);
                    var length = parsed.Count / Mathf.Max(0.0001f, clip.FrameRate);
                    EditorGUILayout.LabelField($"{parsed.Count} frames, {length:0.###}s", EditorStyles.miniLabel);
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Add Clip"))
                    _clips.Add(new ClipDefinition());

                using (new EditorGUI.DisabledScope(component == null))
                {
                    if (GUILayout.Button("Add One Clip Per Row"))
                        AddRowClips(component);
                }
            }
        }

        void AddRowClips(Component component)
        {
            var renderer = ResolveSampleRenderer(component);
            if (renderer == null) return;

            for (var row = 0; row < renderer.Rows; row++)
            {
                var start = row * renderer.Columns;
                _clips.Add(new ClipDefinition
                {
                    Name = $"row_{row}",
                    Frames = $"{start}-{start + renderer.Columns - 1}",
                    FrameRate = 10f,
                    Loop = true
                });
            }
        }

        /// <summary>
        /// One button per known character sheet, filling in its established clip list. A preset's
        /// frame numbers only mean anything on the grid they were authored for, so a preset whose
        /// grid does not match the target is marked and warns before it fills anything.
        /// </summary>
        void DrawPresets(Component component)
        {
            EditorGUILayout.LabelField("Presets", EditorStyles.boldLabel);

            var renderer = ResolveSampleRenderer(component);
            var columns = renderer != null ? renderer.Columns : 0;
            var rows = renderer != null ? renderer.Rows : 0;
            var gridKnown = renderer != null && renderer.Sheet != null;

            using (new EditorGUILayout.HorizontalScope())
            {
                foreach (var preset in FrameClipPresets.All)
                {
                    var matches = gridKnown && preset.Columns == columns && preset.Rows == rows;
                    var label = matches ? $"{preset.Label} ✔" : preset.Label;
                    var tooltip = $"{preset.Grid} grid.\n{preset.Note}" +
                                  (matches ? "\n\nMatches the target sheet." : gridKnown
                                      ? $"\n\nTarget sheet is {columns}x{rows} — frame numbers will not line up."
                                      : "");

                    if (!GUILayout.Button(new GUIContent(label, tooltip), GUILayout.Height(24f))) continue;

                    if (gridKnown && !matches &&
                        !EditorUtility.DisplayDialog("Grid mismatch",
                            $"The {preset.Label} preset is written for a {preset.Grid} sheet, but the target " +
                            $"is {columns}x{rows}.\n\nIts frame numbers will point at the wrong cells. Load it anyway?",
                            "Load anyway", "Cancel"))
                        continue;

                    _pasteText = preset.ClipList;
                    _pasteExpanded = true;
                    _clips.Clear();
                    _clips.AddRange(ParseClipList(preset.ClipList));
                    _selectedPresetNote = preset.Note;
                    GUI.FocusControl(null);
                }
            }

            if (!string.IsNullOrEmpty(_selectedPresetNote))
                EditorGUILayout.HelpBox(_selectedPresetNote, MessageType.Info);
        }

        void DrawPasteList()
        {
            _pasteExpanded = EditorGUILayout.Foldout(_pasteExpanded, "Paste Clip List", true);
            if (!_pasteExpanded) return;

            EditorGUILayout.HelpBox(
                "One clip per line:  name: frames @ fps loop\n" +
                "fps and loop are optional (default 10, looping). 'once' turns looping off.\n" +
                "  walk_forward: 0-7 @ 10 loop\n" +
                "  sit_down_front: 112-116 @ 8 once",
                MessageType.None);

            _pasteText = EditorGUILayout.TextArea(_pasteText, GUILayout.MinHeight(90f));

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Replace List"))
                {
                    _clips.Clear();
                    _clips.AddRange(ParseClipList(_pasteText));
                }

                if (GUILayout.Button("Append To List"))
                    _clips.AddRange(ParseClipList(_pasteText));
            }
        }

        /// <summary>
        /// Parses "name: frames @ fps loop" lines. Lets a whole character's clip set be pasted in
        /// one go rather than typed row by row — the guest alone is 23 clips.
        /// </summary>
        static List<ClipDefinition> ParseClipList(string text)
        {
            var result = new List<ClipDefinition>();
            if (string.IsNullOrWhiteSpace(text)) return result;

            foreach (var rawLine in text.Split('\n'))
            {
                var line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith("#") || line.StartsWith("//")) continue;

                var colon = line.IndexOf(':');
                if (colon <= 0)
                {
                    Debug.LogWarning($"[Frame Clips] Skipped '{line}' — expected 'name: frames'.");
                    continue;
                }

                var definition = new ClipDefinition
                {
                    Name = line.Substring(0, colon).Trim(),
                    FrameRate = 10f,
                    Loop = true
                };

                var remainder = line.Substring(colon + 1).Trim();

                // Trailing loop/once keyword, checked before the rate so both can be omitted.
                if (remainder.EndsWith("once", StringComparison.OrdinalIgnoreCase))
                {
                    definition.Loop = false;
                    remainder = remainder.Substring(0, remainder.Length - 4).Trim();
                }
                else if (remainder.EndsWith("loop", StringComparison.OrdinalIgnoreCase))
                {
                    remainder = remainder.Substring(0, remainder.Length - 4).Trim();
                }

                var at = remainder.IndexOf('@');
                if (at >= 0)
                {
                    var rateText = remainder.Substring(at + 1).Trim();
                    if (float.TryParse(rateText, out var rate) && rate > 0f) definition.FrameRate = rate;
                    remainder = remainder.Substring(0, at).Trim();
                }

                definition.Frames = remainder;

                if (definition.Name.Length == 0 || ParseFrames(definition.Frames).Count == 0)
                {
                    Debug.LogWarning($"[Frame Clips] Skipped '{line}' — no name or no valid frames.");
                    continue;
                }

                result.Add(definition);
            }

            return result;
        }

        void Generate(string relativePath, Type componentType)
        {
            if (!AssetDatabase.IsValidFolder(_outputFolder))
            {
                if (!CreateFolderRecursive(_outputFolder))
                {
                    EditorUtility.DisplayDialog("Frame Clips", $"Could not create '{_outputFolder}'.", "OK");
                    return;
                }
            }

            var created = new List<AnimationClip>();

            foreach (var definition in _clips)
            {
                var frames = ParseFrames(definition.Frames);
                if (frames.Count == 0)
                {
                    Debug.LogWarning($"[Frame Clips] '{definition.Name}' has no valid frames — skipped.");
                    continue;
                }

                var path = $"{_outputFolder}/{SanitizeFileName(definition.Name)}.anim";
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                var isNew = clip == null;
                if (isNew) clip = new AnimationClip();

                clip.name = definition.Name;
                clip.frameRate = Mathf.Max(1f, definition.FrameRate);

                WriteFrameCurve(clip, relativePath, componentType, frames, definition.FrameRate, definition.Loop);

                var settings = AnimationUtility.GetAnimationClipSettings(clip);
                settings.loopTime = definition.Loop;
                AnimationUtility.SetAnimationClipSettings(clip, settings);

                if (isNew) AssetDatabase.CreateAsset(clip, path);
                else EditorUtility.SetDirty(clip);

                created.Add(clip);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (_controller != null) AddStates(created);

            Debug.Log($"[Frame Clips] Wrote {created.Count} clip(s) to {_outputFolder}.");
        }

        static void WriteFrameCurve(AnimationClip clip, string relativePath, Type componentType,
            List<int> frames, float frameRate, bool loop)
        {
            var step = 1f / Mathf.Max(1f, frameRate);
            var keys = new Keyframe[frames.Count + 1];

            for (var i = 0; i < frames.Count; i++)
                keys[i] = StepKey(i * step, frames[i]);

            // A terminating key gives the last frame real duration — without it the clip ends
            // on the final key and that pose is displayed for zero time. On a looping clip it
            // carries the first frame's value, which is exactly where the loop wraps to.
            keys[frames.Count] = StepKey(frames.Count * step, loop ? frames[0] : frames[frames.Count - 1]);

            var curve = new AnimationCurve(keys);
            for (var i = 0; i < curve.length; i++)
            {
                AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Constant);
                AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Constant);
            }

            // DiscreteCurve is the binding the Animation window itself produces for int fields;
            // it keeps the value stepped rather than interpolating between indices.
            var binding = EditorCurveBinding.DiscreteCurve(relativePath, componentType, "Frame");

            // Clear any curve left by a previous generation before writing the new one, so a
            // shortened clip doesn't keep stale trailing keys.
            AnimationUtility.SetEditorCurve(clip, binding, null);
            AnimationUtility.SetEditorCurve(clip, binding, curve);
        }

        static Keyframe StepKey(float time, int value) => new Keyframe(time, value)
        {
            inTangent = float.PositiveInfinity,
            outTangent = float.PositiveInfinity
        };

        void AddStates(List<AnimationClip> clips)
        {
            var layer = _controller.layers[0];
            var machine = layer.stateMachine;

            foreach (var clip in clips)
            {
                var existing = FindState(machine, clip.name);
                if (existing != null)
                {
                    existing.motion = clip;
                    continue;
                }

                var state = machine.AddState(clip.name);
                state.motion = clip;
            }

            EditorUtility.SetDirty(_controller);
            AssetDatabase.SaveAssets();
        }

        static AnimatorState FindState(AnimatorStateMachine machine, string name)
        {
            foreach (var child in machine.states)
            {
                if (child.state != null && child.state.name == name) return child.state;
            }

            return null;
        }

        static Component ResolveFrameComponent(GameObject target, out Type componentType)
        {
            componentType = null;
            if (target == null) return null;

            // Group first: on a layered character the group is the correct binding, and it also
            // has a SheetSpriteRenderer somewhere below it that would otherwise win.
            var group = target.GetComponent<SheetSpriteGroup>();
            if (group != null)
            {
                componentType = typeof(SheetSpriteGroup);
                return group;
            }

            var renderer = target.GetComponent<SheetSpriteRenderer>();
            if (renderer != null)
            {
                componentType = typeof(SheetSpriteRenderer);
                return renderer;
            }

            return null;
        }

        /// <summary>Parses "0-7", "24,25,26" or "8-15,0" into an explicit frame list. Ranges may descend.</summary>
        static List<int> ParseFrames(string text)
        {
            var frames = new List<int>();
            if (string.IsNullOrWhiteSpace(text)) return frames;

            foreach (var chunk in text.Split(','))
            {
                var token = chunk.Trim();
                if (token.Length == 0) continue;

                var dash = token.IndexOf('-', 1);
                if (dash > 0)
                {
                    var startText = token.Substring(0, dash).Trim();
                    var endText = token.Substring(dash + 1).Trim();
                    if (!int.TryParse(startText, out var start) || !int.TryParse(endText, out var end)) continue;

                    var stepDirection = end >= start ? 1 : -1;
                    for (var f = start; ; f += stepDirection)
                    {
                        frames.Add(f);
                        if (f == end) break;
                    }
                }
                else if (int.TryParse(token, out var single))
                {
                    frames.Add(single);
                }
            }

            return frames;
        }

        static bool CreateFolderRecursive(string folder)
        {
            folder = folder.Replace('\\', '/').TrimEnd('/');
            if (AssetDatabase.IsValidFolder(folder)) return true;
            if (!folder.StartsWith("Assets")) return false;

            var parts = folder.Split('/');
            var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }

            AssetDatabase.Refresh();
            return AssetDatabase.IsValidFolder(folder);
        }

        static string ToProjectRelative(string absolute)
        {
            absolute = absolute.Replace('\\', '/');
            var project = Directory.GetCurrentDirectory().Replace('\\', '/');
            return absolute.StartsWith(project) ? absolute.Substring(project.Length).TrimStart('/') : absolute;
        }

        static string SanitizeFileName(string name)
        {
            var builder = new StringBuilder(name.Length);
            foreach (var c in name)
                builder.Append(Array.IndexOf(Path.GetInvalidFileNameChars(), c) >= 0 ? '_' : c);

            var result = builder.ToString().Trim();
            return result.Length == 0 ? "clip" : result;
        }
    }
}
