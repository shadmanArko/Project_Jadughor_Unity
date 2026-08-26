using System.Collections.Generic;
using Systems.MineSystem.BossLairSystem.Model;
using Systems.MineSystem.Mine.Enum;
using UnityEngine;

namespace Systems.MineSystem.BossLairSystem.Scriptable
{
    /// <summary>
    /// Authored distribution deciding whether a mine contains a boss gate at
    /// all, and which boss it belongs to for the current region and site.
    /// </summary>
    [CreateAssetMenu(
        fileName = "BossSpawnTable",
        menuName = "Boss/Boss Spawn Table")]
    public sealed class BossSpawnTableScriptable : ScriptableObject
    {
        [Header("Run Chance")]
        [Tooltip(
            "Chance this mine contains a boss gate at all. Rolled once per " +
            "generated mine, before any boss is chosen, so players do not find " +
            "a lair every run.")]
        [Range(0f, 100f)] [SerializeField] private float bossGateChancePercent = 25f;

        [Header("Distribution")]
        [Tooltip(
            "Boss candidates per region and site. A pairing with no entry has " +
            "no boss, regardless of the run chance above.")]
        [SerializeField] private List<BossSpawnTableEntry> entries = new();

        public float BossGateChancePercent => bossGateChancePercent;

        /// <summary>
        /// Returns the candidates authored for a region and site, or null when
        /// the pairing has no entry.
        /// </summary>
        public IReadOnlyList<BossSpawnCandidate> FindCandidates(
            Region region,
            Site site)
        {
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry != null && entry.region == region && entry.site == site)
                    return entry.candidates;
            }
            return null;
        }

        public bool Validate(out string error)
        {
            var seen = new HashSet<(Region, Site)>();
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry == null)
                {
                    error = $"{name} entry {i} is empty.";
                    return false;
                }
                // A duplicate pairing would make FindCandidates silently ignore
                // everything after the first match.
                if (!seen.Add((entry.region, entry.site)))
                {
                    error =
                        $"{name} has more than one entry for " +
                        $"{entry.region}/{entry.site}.";
                    return false;
                }
                if (!ValidateCandidates(entry, i, out error))
                    return false;
            }

            error = null;
            return true;
        }

        private bool ValidateCandidates(
            BossSpawnTableEntry entry,
            int entryIndex,
            out string error)
        {
            if (entry.candidates == null)
            {
                error = null;
                return true;
            }

            for (var i = 0; i < entry.candidates.Count; i++)
            {
                var candidate = entry.candidates[i];
                if (candidate == null || candidate.profile == null)
                {
                    error =
                        $"{name} entry {entryIndex} " +
                        $"({entry.region}/{entry.site}) candidate {i} has no " +
                        "boss profile.";
                    return false;
                }
                if (!candidate.profile.Validate(out var profileError))
                {
                    error =
                        $"{name} entry {entryIndex} " +
                        $"({entry.region}/{entry.site}) candidate {i}: " +
                        profileError;
                    return false;
                }
            }

            error = null;
            return true;
        }
    }
}
