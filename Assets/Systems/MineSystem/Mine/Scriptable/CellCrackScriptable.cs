using System.Collections.Generic;
using Systems.MineSystem.Mine.Model;
using UnityEngine;

namespace Systems.MineSystem.Mine.Scriptable
{
    [CreateAssetMenu(fileName = "CellCrackScriptable", menuName = "Scriptable/CellCrackScriptable")]
    public class CellCrackScriptable : ScriptableObject
    {
        public List<CellCrackSpriteData> cellCrackSpriteDatas;
    }
}