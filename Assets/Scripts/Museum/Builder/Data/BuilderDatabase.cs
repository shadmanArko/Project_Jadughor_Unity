using System.Collections.Generic;
using UnityEngine;

namespace ProjectMuseum.Builder
{
    /// <summary>
    /// All placeable-object data as one editable asset, populated from the source
    /// JSON by <c>Tools ▸ Project Museum ▸ Import Builder JSON</c>. Each entry pairs
    /// the pure JSON model with an icon <see cref="Texture2D"/> resolved at import
    /// time (so no Resources/AssetDatabase is needed at runtime).
    ///
    /// Flooring is intentionally absent here — those cards come from the live tileset
    /// on <c>MuseumTilePlacementManager</c>, built by the panel controller.
    /// </summary>
    [CreateAssetMenu(fileName = "BuilderDatabase", menuName = "Project Museum/Builder Database")]
    public class BuilderDatabase : ScriptableObject
    {
        [System.Serializable] public class ExhibitEntry { public ExhibitVariation Data; public Texture2D Icon; }
        [System.Serializable] public class DecorationShopEntry { public DecorationShopVariation Data; public Texture2D Icon; }
        [System.Serializable] public class DecorationOtherEntry { public DecorationOtherVariation Data; public Texture2D Icon; }
        [System.Serializable] public class SanitationEntry { public SanitationVariation Data; public Texture2D Icon; }
        [System.Serializable] public class WallpaperEntry { public WallpaperVariation Data; public Texture2D Icon; }

        [SerializeField] private List<ExhibitEntry> exhibits = new();
        [SerializeField] private List<DecorationShopEntry> decorationShops = new();
        [SerializeField] private List<DecorationOtherEntry> decorationOthers = new();
        [SerializeField] private List<SanitationEntry> sanitations = new();
        [SerializeField] private List<WallpaperEntry> wallpapers = new();

        /// <summary>
        /// Build the display cards for a category. Returns an empty list for Flooring
        /// (handled by the panel) and for any category with no data.
        /// </summary>
        public List<BuilderCardData> GetCards(BuilderCardType type)
        {
            var cards = new List<BuilderCardData>();
            switch (type)
            {
                case BuilderCardType.Exhibit:
                    foreach (var e in exhibits)
                        if (e?.Data != null)
                            cards.Add(new BuilderCardData(type, e.Data.VariationName,
                                Money(e.Data.Price), Icon(e.Icon, e.Data.NumberOfFrames)));
                    break;

                case BuilderCardType.DecorationShop:
                    foreach (var e in decorationShops)
                        if (e?.Data != null)
                            cards.Add(new BuilderCardData(type, e.Data.VariationName,
                                Money(e.Data.PlacementCost), Icon(e.Icon, e.Data.NumberOfFrames)));
                    break;

                case BuilderCardType.DecorationOther:
                    foreach (var e in decorationOthers)
                        if (e?.Data != null)
                            cards.Add(new BuilderCardData(type, e.Data.VariationName,
                                Money(e.Data.PlacementCost), Icon(e.Icon, e.Data.NumberOfFrames)));
                    break;

                case BuilderCardType.Sanitation:
                    foreach (var e in sanitations)
                        if (e?.Data != null)
                            cards.Add(new BuilderCardData(type, e.Data.SanitationId,
                                Money(e.Data.PlacementCost), Icon(e.Icon, e.Data.NumberOfFrames)));
                    break;

                case BuilderCardType.Wallpaper:
                    foreach (var e in wallpapers)
                        if (e?.Data != null)
                            cards.Add(new BuilderCardData(type, e.Data.VariationName,
                                Money(e.Data.Price), Icon(e.Icon, e.Data.NumberOfFrames)));
                    break;
            }
            return cards;
        }

        private static Sprite Icon(Texture2D tex, int frames) => BuilderSpriteUtil.FirstFrameSprite(tex, frames);
        private static string Money(float price) => $"${price:0.##}";

#if UNITY_EDITOR
        public void SetExhibits(List<ExhibitEntry> v) => exhibits = v;
        public void SetDecorationShops(List<DecorationShopEntry> v) => decorationShops = v;
        public void SetDecorationOthers(List<DecorationOtherEntry> v) => decorationOthers = v;
        public void SetSanitations(List<SanitationEntry> v) => sanitations = v;
        public void SetWallpapers(List<WallpaperEntry> v) => wallpapers = v;
#endif
    }
}
