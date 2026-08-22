using System.Collections.Generic;
using UnityEngine;

namespace ProjectMuseum.Characters
{
    /// <summary>
    /// One animated <see cref="Frame"/> driving a stack of <see cref="SheetSpriteRenderer"/>
    /// layers — the Unity version of the Godot guest, where Shadow / Skin / Eye / Hair / Shoe /
    /// Pant / Cloth / OverCloth all keyed the same frame numbers in lockstep.
    ///
    /// Put the clip's frame track on this component instead of on eight separate sprites: one
    /// track per clip rather than eight, and adding a layer later doesn't mean re-keying
    /// every animation.
    /// </summary>
    [ExecuteAlways]
    [AddComponentMenu("Project Museum/Sheet Sprite Group")]
    public class SheetSpriteGroup : MonoBehaviour
    {
        [System.Serializable]
        public class Layer
        {
            public SheetSpriteRenderer Renderer;

            [Tooltip("Added to the group frame for this layer only. Use it when a layer's own " +
                     "sheet puts the same pose at a different index.")]
            public int FrameOffset;
        }

        [Header("Frame")]
        [Tooltip("Animate this. Broadcast to every layer below, plus each layer's own offset.")]
        public int Frame;

        [Tooltip("Mirrors every layer at once (guests turning around).")]
        public bool FlipX;

        [Header("Layers")]
        [Tooltip("Draw order is list order: index 0 renders furthest back.")]
        [SerializeField] List<Layer> _layers = new List<Layer>();

        [Tooltip("Rewrites each layer's SpriteRenderer sorting order from its list position, " +
                 "so reordering the list reorders the visual stack.")]
        [SerializeField] bool _driveSortingOrder = true;

        [SerializeField] int _sortingOrderBase;

        int _appliedFrame = int.MinValue;
        bool _appliedFlipX;

        public IReadOnlyList<Layer> Layers => _layers;

        void OnEnable()
        {
            _appliedFrame = int.MinValue;
            if (_driveSortingOrder) ApplySortingOrder();
            Apply(true);
        }

        void OnValidate()
        {
            _appliedFrame = int.MinValue;
            if (_driveSortingOrder) ApplySortingOrder();
            Apply(true);
        }

        void LateUpdate() => Apply();

        /// <summary>Pushes the group frame down to every layer. Early-outs on an unchanged frame.</summary>
        public void Apply(bool force = false)
        {
            if (!force && Frame == _appliedFrame && FlipX == _appliedFlipX) return;

            for (var i = 0; i < _layers.Count; i++)
            {
                var layer = _layers[i];
                if (layer?.Renderer == null) continue;

                layer.Renderer.FlipX = FlipX;
                layer.Renderer.SetFrame(Frame + layer.FrameOffset);
            }

            _appliedFrame = Frame;
            _appliedFlipX = FlipX;
        }

        /// <summary>Assigns sorting orders from list order. Called on enable/validate, not per frame.</summary>
        public void ApplySortingOrder()
        {
            for (var i = 0; i < _layers.Count; i++)
            {
                var layer = _layers[i];
                if (layer?.Renderer == null) continue;
                layer.Renderer.Renderer.sortingOrder = _sortingOrderBase + i;
            }
        }

        /// <summary>
        /// Swaps one layer's sheet, keeping the current pose. This is the customisation entry
        /// point — pass the chosen hair/skin/cloth variant for a spawned guest.
        /// </summary>
        public void SetLayerSheet(int layerIndex, Texture2D sheet)
        {
            if (layerIndex < 0 || layerIndex >= _layers.Count) return;
            var layer = _layers[layerIndex];
            if (layer?.Renderer == null) return;

            layer.Renderer.Sheet = sheet;
            layer.Renderer.SetFrame(Frame + layer.FrameOffset);
        }

        [ContextMenu("Collect Child Layers")]
        public void CollectChildLayers()
        {
            var found = GetComponentsInChildren<SheetSpriteRenderer>(true);
            _layers.Clear();
            foreach (var renderer in found)
                _layers.Add(new Layer { Renderer = renderer });

            if (_driveSortingOrder) ApplySortingOrder();
            Apply(true);
        }
    }
}
