using System.Collections.Generic;
using UnityEngine;

namespace ProjectMuseum.Builder
{
    /// <summary>
    /// Builds a card icon showing only the FIRST frame of a horizontal multi-frame
    /// sprite sheet (mirrors Godot's AtlasTexture region). Works regardless of the
    /// texture's import "sprite mode" and does NOT require Read/Write to be enabled
    /// (only pixel reads would). Results are cached per (texture, frames).
    /// </summary>
    public static class BuilderSpriteUtil
    {
        private static readonly Dictionary<(Texture2D, int), Sprite> _cache = new();

        public static Sprite FirstFrameSprite(Texture2D tex, int numberOfFrames)
        {
            if (tex == null) return null;

            int frames = Mathf.Max(1, numberOfFrames);
            var key = (tex, frames);
            if (_cache.TryGetValue(key, out Sprite cached) && cached != null)
                return cached;

            float frameWidth = tex.width / (float)frames;
            var rect = new Rect(0f, 0f, frameWidth, tex.height);
            var sprite = Sprite.Create(tex, rect, new Vector2(0.5f, 0.5f), 100f);
            _cache[key] = sprite;
            return sprite;
        }
    }
}
