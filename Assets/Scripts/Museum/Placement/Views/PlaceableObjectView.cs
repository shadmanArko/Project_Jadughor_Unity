using ProjectMuseum.Data;
using UnityEngine;

namespace ProjectMuseum.Builder
{
    /// <summary>
    /// Base component for a placed museum object's world prefab. Holds identity
    /// (Id / Type / VariationName) and the renderers used for ghost tinting.
    ///
    /// IMPORTANT: real setup happens in <see cref="Initialize"/>, called explicitly
    /// by <c>MuseumObjectPlacementSystem</c> — NOT in Awake. That's what lets the
    /// exact same prefab be instantiated as a ghost preview (tinted, never
    /// Initialized) with zero risk of placement-only logic running early.
    ///
    /// Subclass per category to add type-specific behaviour later — see
    /// <see cref="ExhibitObjectView"/>, <see cref="ShopObjectView"/>,
    /// <see cref="DecorationObjectView"/>, <see cref="SanitationObjectView"/>.
    /// </summary>
    public class PlaceableObjectView : MonoBehaviour, IInteractable
    {
        [Tooltip("Renderers tinted while this instance is a ghost preview. Leave " +
                 "empty to auto-collect every SpriteRenderer under this object.")]
        [SerializeField] private SpriteRenderer[] renderers;

        [Tooltip("The renderer whose sprite gets swapped to the specific variation's " +
                 "artwork (see ApplyVariationSprite). Leave empty to use the first " +
                 "renderer above/auto-collected — fine for a one-sprite prefab.")]
        [SerializeField] private SpriteRenderer primaryRenderer;

        public string Id { get; private set; }
        public BuilderCardType Type { get; private set; }
        public string VariationName { get; private set; }
        public bool IsPlaced { get; private set; }

        protected virtual void Awake()
        {
            if (renderers == null || renderers.Length == 0)
                renderers = GetComponentsInChildren<SpriteRenderer>(true);
        }

        /// <summary>Turn this instance into the real placed object. Called once, on placement.</summary>
        public void Initialize(PlacedObjectData data)
        {
            Id = data.Id;
            Type = data.Type;
            VariationName = data.VariationName;
            IsPlaced = true;
            ClearGhostTint();
            OnInitialized(data);
        }

        /// <summary>Hook for subclasses — runs once identity fields are set.</summary>
        protected virtual void OnInitialized(PlacedObjectData data) { }

        /// <summary>
        /// Left-click handler (via <c>MuseumInteractionSystem</c>). Base does
        /// nothing; subclasses override — e.g. <c>ExhibitObjectView</c> opens the
        /// exhibit editor. Only responds once actually placed (never for a ghost).
        /// </summary>
        public virtual void Interact() { }

        /// <summary>True if this object's art covers the given world point (for click hit-testing).</summary>
        public bool ContainsWorldPoint(Vector3 worldPoint)
        {
            if (renderers == null) return false;
            foreach (SpriteRenderer r in renderers)
            {
                if (r == null || !r.enabled) continue;
                Bounds b = r.bounds;
                if (worldPoint.x >= b.min.x && worldPoint.x <= b.max.x &&
                    worldPoint.y >= b.min.y && worldPoint.y <= b.max.y)
                    return true;
            }
            return false;
        }

        /// <summary>Representative sorting order (all this object's renderers share one).</summary>
        public int SortingOrder =>
            renderers != null && renderers.Length > 0 && renderers[0] != null
                ? renderers[0].sortingOrder : 0;

        /// <summary>
        /// Swap in the artwork for the SPECIFIC variation being placed. The prefab is
        /// shared across every variation of its footprint size (e.g. one 1x1
        /// DecorationOther prefab for every plant color) — this is what makes each
        /// instance actually look like the object the player picked. Called for both
        /// ghosts and real placements, before <see cref="Initialize"/> for the latter.
        ///
        /// The new sprite is auto-sized to the SAME world width as the renderer's
        /// placeholder sprite by choosing its pixels-per-unit — NOT by scaling the
        /// transform. Scaling the transform would also resize any child renderers
        /// (e.g. an exhibit's glass case, shadows), which is wrong. Aspect ratio is
        /// preserved (uniform PPU), so the art never distorts. Give the target
        /// renderer a placeholder sprite sized correctly for its footprint so there's
        /// a reference width to match.
        /// </summary>
        public void ApplyVariationSprite(Sprite sprite)
        {
            if (sprite == null) return;
            SpriteRenderer target = primaryRenderer != null ? primaryRenderer
                : (renderers != null && renderers.Length > 0 ? renderers[0] : null);
            if (target == null) return;

            float placeholderWidth = target.sprite != null ? target.sprite.bounds.size.x : 0f;

            if (placeholderWidth > 0f && sprite.texture != null && sprite.rect.width > 0f)
            {
                // pixels-per-unit that makes the new sprite's world width == the
                // placeholder's. bounds.size.x = rect.width / ppu  →  ppu = rect.width / width.
                float ppu = sprite.rect.width / placeholderWidth;
                Vector2 pivot = new Vector2(
                    sprite.pivot.x / sprite.rect.width,
                    sprite.rect.height > 0f ? sprite.pivot.y / sprite.rect.height : 0f);
                target.sprite = Sprite.Create(sprite.texture, sprite.rect, pivot, ppu);
            }
            else
            {
                target.sprite = sprite; // no placeholder / no texture — use as-is
            }
            // NOTE: transform.localScale is intentionally left untouched.
        }

        public void SetGhostTint(Color tint)
        {
            foreach (SpriteRenderer r in renderers)
                if (r != null) r.color = tint;
        }

        public void ClearGhostTint()
        {
            foreach (SpriteRenderer r in renderers)
                if (r != null) r.color = Color.white;
        }
    }
}
