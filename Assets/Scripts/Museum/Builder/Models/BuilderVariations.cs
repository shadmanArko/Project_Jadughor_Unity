using System;

namespace ProjectMuseum.Builder
{
    // Placeable-object data, ported 1:1 from the Godot / ASP.NET models so the same
    // JSON deserialises with Unity's JsonUtility. Field names are PascalCase on
    // purpose — they must match the JSON keys. These stay PURE data (no sprites);
    // the resolved icon lives on the database entry wrapper, not here.

    /// <summary>A museum exhibit display case.</summary>
    [Serializable]
    public class ExhibitVariation
    {
        public string VariationName;
        public float Price;
        public string ExhibitDecoration;
        public string ExhibitSize;
        public int NumberOfTilesNeeded;
        public string TilesExtendInDirection;
        public bool IsHangingExhibit;
        public int NumberOfFrames;
        public bool IsWallExhibit;

        // Not present in the original Godot data (which only had NumberOfTilesNeeded —
        // ambiguous for anything wider than 1 tile). Set explicitly per variation; 0
        // means "not set" and BuilderDatabase falls back to NumberOfTilesNeeded × 1.
        public int WidthInTiles;
        public int LengthInTiles;
    }

    /// <summary>A vendor machine / shop decoration.</summary>
    [Serializable]
    public class DecorationShopVariation
    {
        public string VariationName;
        public int NumberOfTilesNeeded;
        public bool IsDrinkShop;
        public bool IsFoodShop;
        public bool IsSouvenirShop;
        public float BasePricePerItem;
        public int LengthInTiles;
        public int WidthInTiles;
        public int NumberOfFrames;
        public int BeautyRating;
        public float PlacementCost;
    }

    /// <summary>A non-shop decoration (plants, furniture, …).</summary>
    [Serializable]
    public class DecorationOtherVariation
    {
        public string VariationName;
        public int NumberOfTilesNeeded;
        public int BeautyRating;
        public int NumberOfFrames;
        public float PlacementCost;

        // Same fallback as ExhibitVariation — 0 means "not set".
        public int WidthInTiles;
        public int LengthInTiles;
    }

    /// <summary>A sanitation fixture (toilets, etc.). Note: id/price fields differ.</summary>
    [Serializable]
    public class SanitationVariation
    {
        public string SanitationId;
        public float PlacementCost;
        public int CanAccomodate;
        public int LengthInTiles;
        public int WidthInTiles;
        public int NumberOfTilesNeeded;
        public int NumberOfFrames;
        public float BeautyRating;
    }

    /// <summary>A wallpaper option applied to walls.</summary>
    [Serializable]
    public class WallpaperVariation
    {
        public string VariationName;
        public int NumberOfFrames;
        public int Price;
    }
}
