using System;
using System.Collections.Generic;
using UnityEngine;
using ProjectMuseum.Builder;

namespace ProjectMuseum.Data
{
    /// <summary>
    /// The complete persistent state of the museum — tiles, placed objects,
    /// expansion progress and general info. This exact block is what will be
    /// embedded in the master SaveData later; keep it plain and JsonUtility-safe
    /// (no dictionaries — runtime lookups are rebuilt by <c>MuseumDataModel</c>).
    /// </summary>
    [Serializable]
    public class MuseumData
    {
        public MuseumInfo Info = new MuseumInfo();

        /// <summary>Lattice coords of developed chunks. A new game starts with (0,0).</summary>
        public List<Vector2Int> DevelopedChunks = new List<Vector2Int>();

        /// <summary>One record per tile of every developed chunk (undeveloped land has none).</summary>
        public List<MuseumTileData> Tiles = new List<MuseumTileData>();

        /// <summary>Every placed object of every category — the single placement list.</summary>
        public List<PlacedObjectData> PlacedObjects = new List<PlacedObjectData>();

        /// <summary>
        /// Exhibit-only side data (artifact slots), keyed by the exhibit's
        /// <see cref="PlacedObjectData.Id"/>. Created together with the placement.
        /// </summary>
        public List<ExhibitData> Exhibits = new List<ExhibitData>();

        /// <summary>
        /// Every registered wall segment and its current wallpaper. Registered by
        /// <c>MuseumWallpaperSystem</c> from the scene's wall containers.
        /// </summary>
        public List<WallData> Walls = new List<WallData>();
    }

    /// <summary>
    /// One wall segment. <see cref="Id"/> is derived from the scene hierarchy
    /// ("&lt;container&gt;/&lt;childIndex&gt;") so it's stable across sessions.
    /// Empty <see cref="WallpaperName"/> = bare wall (original sprite).
    /// </summary>
    [Serializable]
    public class WallData
    {
        public string Id;
        public string WallpaperName = "";
    }

    /// <summary>General museum state (name, money — ticket counter etc. later).</summary>
    [Serializable]
    public class MuseumInfo
    {
        public string Name = "My Museum";
        public float Money = 1000f;
    }

    /// <summary>One grid cell of the museum floor.</summary>
    [Serializable]
    public class MuseumTileData
    {
        public int X;
        public int Y;
        /// <summary>Which floor tile is painted here (TileBase asset name).</summary>
        public string TileVariationName;
        public bool Walkable = true;
        /// <summary>Id of the <see cref="PlacedObjectData"/> standing on this tile ("" = free).</summary>
        public string OccupantId = "";

        public Vector2Int Cell => new Vector2Int(X, Y);
        public bool IsOccupied => !string.IsNullOrEmpty(OccupantId);
    }

    /// <summary>
    /// One placed object of any category. Carries its own identity, type, anchor
    /// (front tile) and footprint, so placement/removal/loading is a single code
    /// path. Static properties (price, frames, beauty…) stay in
    /// <c>BuilderDatabase</c>, keyed by <see cref="VariationName"/>.
    /// </summary>
    [Serializable]
    public class PlacedObjectData
    {
        public string Id;                 // GUID
        public BuilderCardType Type;      // Exhibit / DecorationShop / DecorationOther / Sanitation
        public string VariationName;      // key into BuilderDatabase
        public int X;                     // anchor = front tile (where the player clicked)
        public int Y;
        public int WidthInTiles = 1;      // footprint copied from the variation at placement
        public int LengthInTiles = 1;
        public int RotationFrame;
        public List<Vector2Int> OccupiedTiles = new List<Vector2Int>();

        public Vector2Int AnchorCell => new Vector2Int(X, Y);
    }

    /// <summary>
    /// Exhibit-only data: which artifacts sit in this exhibit. <see cref="Id"/> is
    /// the same GUID as the exhibit's <see cref="PlacedObjectData.Id"/>.
    /// Artifact placement itself comes in a later phase.
    /// </summary>
    [Serializable]
    public class ExhibitData
    {
        public string Id;
        public List<string> ArtifactIds = new List<string>();
    }
}
