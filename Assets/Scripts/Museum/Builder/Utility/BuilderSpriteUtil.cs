using System.Collections.Generic;
using UnityEngine;

namespace ProjectMuseum.Builder
{
    /// <summary>
    /// Builds a sprite showing one frame of a horizontal multi-frame sprite sheet
    /// (mirrors Godot's AtlasTexture/Sprite2D.Frame region). Works regardless of the
    /// texture's import "sprite mode" and does NOT require Read/Write to be enabled
    /// (only pixel reads would). Results are cached per (texture, frames, frameIndex, pivot).
    /// </summary>
    public static class BuilderSpriteUtil
    {
        /// <summary>Pivot for world-placed objects: bottom-center, so an object's
        /// base sits ON its tile instead of the tile passing through its middle.</summary>
        public static readonly Vector2 BottomCenterPivot = new Vector2(0.5f, 0f);

        private static readonly Dictionary<(Texture2D, int, int, Vector2), Sprite> _cache = new();

        /// <summary>Frame 0, center-pivoted — used for UI card icons (pivot doesn't affect Image rendering).</summary>
        public static Sprite FirstFrameSprite(Texture2D tex, int numberOfFrames) =>
            FrameSprite(tex, numberOfFrames, 0, new Vector2(0.5f, 0.5f));

        /// <summary>Frame 0 at a given pivot — kept for callers that don't rotate.</summary>
        public static Sprite FirstFrameSprite(Texture2D tex, int numberOfFrames, Vector2 pivot) =>
            FrameSprite(tex, numberOfFrames, 0, pivot);

        /// <summary>
        /// Crop a specific frame (e.g. a rotation state — Godot's Sprite2D.Frame
        /// convention: frame index selects a horizontal slice of the sheet, same
        /// count as NumberOfFrames). Index is clamped/wrapped into range.
        /// </summary>
        public static Sprite FrameSprite(Texture2D tex, int numberOfFrames, int frameIndex, Vector2 pivot)
        {
            if (tex == null) return null;

            int frames = Mathf.Max(1, numberOfFrames);
            int index = ((frameIndex % frames) + frames) % frames; // safe for any int, incl. negative

            var key = (tex, frames, index, pivot);
            if (_cache.TryGetValue(key, out Sprite cached) && cached != null)
                return cached;

            float frameWidth = tex.width / (float)frames;
            var rect = new Rect(frameWidth * index, 0f, frameWidth, tex.height);
            var sprite = Sprite.Create(tex, rect, pivot, 100f);
            _cache[key] = sprite;
            return sprite;
        }
    }
}
