using System;
using System.Collections.Generic;
using Systems.MineSystem.Mine.Enum;
using UnityEngine;

namespace Systems.MineSystem.BossLairSystem.Model
{
    /// <summary>
    /// Boss candidates available for one region and site pairing.
    /// </summary>
    [Serializable]
    public sealed class BossSpawnTableEntry
    {
        [Tooltip("Region this entry applies to.")]
        public Region region;

        [Tooltip("Site this entry applies to.")]
        public Site site;

        [Tooltip(
            "Bosses that can appear here, rolled in order. Leave empty to make " +
            "this region and site pairing boss-free.")]
        public List<BossSpawnCandidate> candidates = new();
    }
}
