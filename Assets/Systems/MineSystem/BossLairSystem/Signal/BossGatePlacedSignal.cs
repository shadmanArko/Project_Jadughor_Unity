using Systems.MineSystem.BossLairSystem.Scriptable;
using UnityEngine;

namespace Systems.MineSystem.BossLairSystem.Signal
{
    /// <summary>
    /// Raised when a boss gate has been placed in the freshly generated mine.
    /// Absent on runs where the boss chance did not hit.
    /// </summary>
    public readonly struct BossGatePlacedSignal
    {
        public BossGatePlacedSignal(Vector3Int cell, BossProfileScriptable profile)
        {
            Cell = cell;
            Profile = profile;
        }

        public Vector3Int Cell { get; }
        public BossProfileScriptable Profile { get; }
    }
}
