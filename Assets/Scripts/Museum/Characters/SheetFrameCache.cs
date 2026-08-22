using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectMuseum.Characters
{
    /// <summary>
    /// Slices sheet textures into <see cref="Sprite"/> grids on demand and shares the
    /// result between every renderer using the same (texture, grid, pivot, ppu) combo.
    ///
    /// Why a cache: a guest is 8 layered sprites and a crowd is dozens of guests. Without
    /// sharing, every guest would allocate its own Sprite per frame per layer (hundreds of
    /// native objects that never get collected). With it, a 8x4 sheet costs exactly 32
    /// Sprites no matter how many renderers point at it.
    ///
    /// Frame indices are row-major, left-to-right then top-to-bottom, matching Godot's
    /// <c>Sprite2D.frame</c> so existing frame numbers port over unchanged.
    /// </summary>
    public static class SheetFrameCache
    {
        readonly struct Key : IEquatable<Key>
        {
            readonly int _texture;
            readonly int _hFrames;
            readonly int _vFrames;
            readonly float _pivotX;
            readonly float _pivotY;
            readonly float _pixelsPerUnit;

            public Key(Texture2D texture, int hFrames, int vFrames, Vector2 pivot, float pixelsPerUnit)
            {
                _texture = texture.GetInstanceID();
                _hFrames = hFrames;
                _vFrames = vFrames;
                _pivotX = pivot.x;
                _pivotY = pivot.y;
                _pixelsPerUnit = pixelsPerUnit;
            }

            public bool Equals(Key other) =>
                _texture == other._texture && _hFrames == other._hFrames && _vFrames == other._vFrames &&
                _pivotX == other._pivotX && _pivotY == other._pivotY && _pixelsPerUnit == other._pixelsPerUnit;

            public override bool Equals(object obj) => obj is Key other && Equals(other);

            public override int GetHashCode()
            {
                var hash = _texture;
                hash = (hash * 397) ^ _hFrames;
                hash = (hash * 397) ^ _vFrames;
                hash = (hash * 397) ^ _pivotX.GetHashCode();
                hash = (hash * 397) ^ _pivotY.GetHashCode();
                hash = (hash * 397) ^ _pixelsPerUnit.GetHashCode();
                return hash;
            }
        }

        static readonly Dictionary<Key, Sprite[]> Sheets = new Dictionary<Key, Sprite[]>();

        /// <summary>
        /// Returns the sliced frames for a sheet, building them on first request.
        /// Never returns null for a valid texture; the array length is hFrames * vFrames.
        /// </summary>
        public static Sprite[] Get(Texture2D sheet, int hFrames, int vFrames, Vector2 pivot, float pixelsPerUnit)
        {
            if (sheet == null) return null;
            if (hFrames < 1) hFrames = 1;
            if (vFrames < 1) vFrames = 1;
            if (pixelsPerUnit <= 0f) pixelsPerUnit = 1f;

            var key = new Key(sheet, hFrames, vFrames, pivot, pixelsPerUnit);
            if (Sheets.TryGetValue(key, out var cached) && cached.Length > 0 && cached[0] != null)
                return cached;

            var frames = Slice(sheet, hFrames, vFrames, pivot, pixelsPerUnit);
            Sheets[key] = frames;
            return frames;
        }

        static Sprite[] Slice(Texture2D sheet, int hFrames, int vFrames, Vector2 pivot, float pixelsPerUnit)
        {
            // Integer cell size; a sheet that doesn't divide evenly loses the remainder
            // pixels on the right/bottom edge, same as Godot's grid slicing.
            var cellWidth = sheet.width / hFrames;
            var cellHeight = sheet.height / vFrames;
            var frames = new Sprite[hFrames * vFrames];

            if (cellWidth <= 0 || cellHeight <= 0)
            {
                Debug.LogWarning($"[SheetFrameCache] '{sheet.name}' is {sheet.width}x{sheet.height}, too small for a {hFrames}x{vFrames} grid.");
                return frames;
            }

            for (var row = 0; row < vFrames; row++)
            {
                for (var col = 0; col < hFrames; col++)
                {
                    // Frame 0 is the top-left cell, but Unity's texture space starts at the
                    // bottom-left, so rows are addressed from the top down.
                    var rect = new Rect(col * cellWidth, sheet.height - (row + 1) * cellHeight, cellWidth, cellHeight);
                    var index = row * hFrames + col;

                    // FullRect: a tight mesh would run alpha analysis per cell and give each
                    // frame a different vertex count, which shows up as sub-pixel jitter on
                    // pixel art. A quad is also the cheaper mesh to batch.
                    var sprite = Sprite.Create(sheet, rect, pivot, pixelsPerUnit, 0, SpriteMeshType.FullRect);
                    sprite.name = $"{sheet.name}_{index}";
                    sprite.hideFlags = HideFlags.HideAndDontSave;
                    frames[index] = sprite;
                }
            }

            return frames;
        }

        /// <summary>Destroys every generated sprite. Called automatically on quit and on editor reload.</summary>
        public static void Clear()
        {
            foreach (var frames in Sheets.Values)
            {
                if (frames == null) continue;
                for (var i = 0; i < frames.Length; i++)
                {
                    if (frames[i] == null) continue;
#if UNITY_EDITOR
                    if (!Application.isPlaying) UnityEngine.Object.DestroyImmediate(frames[i]);
                    else UnityEngine.Object.Destroy(frames[i]);
#else
                    UnityEngine.Object.Destroy(frames[i]);
#endif
                }
            }

            Sheets.Clear();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void HookRuntimeCleanup()
        {
            Clear();
            Application.quitting -= Clear;
            Application.quitting += Clear;
        }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        static void HookEditorCleanup()
        {
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= Clear;
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += Clear;
        }
#endif
    }
}
