using System;
using System.Collections.Generic;
using Systems.MineSystem.Mine.Enum;
using UnityEngine;

namespace ProjectMuseum.MapSelection
{
    /// <summary>
    /// What the Digging &amp; Permits map panel lists: regions (cycled with the arrows) and the
    /// dig sites in each. Edit the asset (Assets/GameData/MapSelectionData.asset) to add regions
    /// or sites, or to lock one — no code changes.
    ///
    /// Godot shipped one entry: NORTH AMERICA → "Possible Battle Site", loading the mine with
    /// region America / site Battle Site. That is the default content.
    /// </summary>
    [CreateAssetMenu(fileName = "MapSelectionData", menuName = "Project Museum/Map Selection Data")]
    public class MapSelectionData : ScriptableObject
    {
        public List<MapRegion> regions = new();
    }

    [Serializable]
    public class MapRegion
    {
        [Tooltip("Shown in the panel's title bar, e.g. NORTH AMERICA.")]
        public string displayName = "NORTH AMERICA";
        [Tooltip("Region the mine generates artifacts for.")]
        public Region region = Region.America;
        public List<MapSite> sites = new();
    }

    [Serializable]
    public class MapSite
    {
        [Tooltip("Button text, e.g. Possible Battle Site.")]
        public string displayName = "Possible Battle Site";
        [Tooltip("Mine site type (drives artifacts, cell art, etc.).")]
        public Site site = Site.BattleSite;
        [Tooltip("Unavailable sites are listed but can't be clicked (no permit yet).")]
        public bool available = true;
    }
}
