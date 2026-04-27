using System.Collections.Generic;
using Systems.MineSystem.MineGenerationSystem.Model;
using UnityEngine;

namespace Systems.MineSystem.Mine.Scriptable
{
    [CreateAssetMenu(fileName = "MineTileScriptable", menuName = "Scriptable/MineTileScriptable")]
    public class MineRegionTileScriptable : ScriptableObject
    {
        public List<MineRegionTiles> regionTiles;
    }
}