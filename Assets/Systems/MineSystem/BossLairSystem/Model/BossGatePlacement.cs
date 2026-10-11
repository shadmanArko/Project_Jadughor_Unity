using Systems.MineSystem.BossLairSystem.Scriptable;
using UnityEngine;

namespace Systems.MineSystem.BossLairSystem.Model
{
    /// <summary>
    /// Where a boss gate was placed in the mine and which boss it leads to.
    /// Data only.
    /// </summary>
    public readonly struct BossGatePlacement
    {
        public BossGatePlacement(Vector3Int cell, BossProfileScriptable profile)
        {
            Cell = cell;
            Profile = profile;
        }

        /// <summary>Mine grid cell the gate occupies.</summary>
        public Vector3Int Cell { get; }

        public BossProfileScriptable Profile { get; }

        public bool IsValid => Profile != null;
    }
}
