using System.Collections.Generic;
using Systems.MineSystem.MineGenerationSystem.Model;
using UnityEngine;

namespace Systems.MineSystem.Mine.Scriptable
{
    [CreateAssetMenu(fileName = "MineRegionalTileScriptable", menuName = "Scriptable/MineRegionalTileScriptable")]
    public class MineRegionalTileScriptable : ScriptableObject
    {
        public List<MineRegionalTiles> regionTiles;
    }
}