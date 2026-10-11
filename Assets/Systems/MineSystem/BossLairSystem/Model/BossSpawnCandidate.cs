using System;
using Systems.MineSystem.BossLairSystem.Scriptable;
using UnityEngine;

namespace Systems.MineSystem.BossLairSystem.Model
{
    /// <summary>
    /// One boss that may appear for a given region and site, with its own
    /// independent spawn chance.
    /// </summary>
    [Serializable]
    public sealed class BossSpawnCandidate
    {
        [Tooltip("Boss profile offered by this entry.")]
        public BossProfileScriptable profile;

        [Tooltip(
            "Chance this boss is chosen, rolled independently and in list " +
            "order. The first candidate that succeeds wins, so put the rarest " +
            "boss first.")]
        [Range(0f, 100f)] public float spawnChancePercent = 100f;
    }
}
