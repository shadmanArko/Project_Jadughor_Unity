using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectMuseum.Builder
{
    /// <summary>
    /// Maps a placeable-object CATEGORY + FOOTPRINT SIZE (not the individual
    /// variation) to the prefab used for both its ghost preview and its real placed
    /// instance. One prefab is shared by every variation of that size — e.g. a
    /// single "1x1 DecorationOther" prefab is reused for every plant color; the
    /// specific look comes from <see cref="PlaceableObjectView.ApplyVariationSprite"/>
    /// swapping in that variation's artwork after instantiating.
    ///
    /// Assign one entry per (Type, WidthInTiles × LengthInTiles) you've built a
    /// prefab for — e.g. Exhibit 1x1, Exhibit 2x2, DecorationShop 1x1, etc.
    ///
    /// Flooring/Wallpaper are NOT configured here — tiles are painted directly by
    /// <c>MuseumTilePlacementManager</c>, wallpapers are sprite-swapped by
    /// <c>MuseumWallpaperSystem</c>.
    /// </summary>
    [CreateAssetMenu(fileName = "PlaceablePrefabConfig", menuName = "Project Museum/Placeable Prefab Config")]
    public class PlaceablePrefabConfig : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public BuilderCardType Type;
            [Tooltip("Footprint this prefab is built for, e.g. 1x1, 1x2, 2x1, 2x2.")]
            public int WidthInTiles = 1;
            public int LengthInTiles = 1;
            [Tooltip("Should have a PlaceableObjectView (or a subclass) on its root. " +
                     "Its sprite is swapped per variation at spawn time.")]
            public GameObject Prefab;
        }

        [SerializeField] private List<Entry> entries = new List<Entry>();

        /// <summary>Exact (Type, WidthInTiles, LengthInTiles) match. Null if none configured.</summary>
        public GameObject GetPrefab(BuilderCardType type, int widthInTiles, int lengthInTiles)
        {
            foreach (Entry e in entries)
                if (e != null && e.Type == type &&
                    e.WidthInTiles == widthInTiles && e.LengthInTiles == lengthInTiles)
                    return e.Prefab;

            Debug.LogWarning($"[PlaceablePrefabConfig] No prefab for {type} " +
                $"{widthInTiles}x{lengthInTiles} — configured sizes for {type}: " +
                $"{string.Join(", ", ConfiguredSizes(type))}");
            return null;
        }

        private IEnumerable<string> ConfiguredSizes(BuilderCardType type)
        {
            foreach (Entry e in entries)
                if (e != null && e.Type == type)
                    yield return $"{e.WidthInTiles}x{e.LengthInTiles}";
        }
    }
}
