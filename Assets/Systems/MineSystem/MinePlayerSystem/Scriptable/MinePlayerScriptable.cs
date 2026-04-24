using Systems.MineSystem.MinePlayerSystem.Model;
using UnityEngine;

namespace Systems.MineSystem.MinePlayerSystem.Scriptable
{
    [CreateAssetMenu(fileName = "MinePlayerScriptable", menuName = "Scriptable/MinePlayerScriptable")]
    public class MinePlayerScriptable : ScriptableObject
    {
        public MinePlayerData playerData; 
    }
}