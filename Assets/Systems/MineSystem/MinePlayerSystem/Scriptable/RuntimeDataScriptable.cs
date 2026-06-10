using UniRx;
using UnityEngine;

namespace Systems.MineSystem.MinePlayerSystem.Scriptable
{
    [CreateAssetMenu(fileName = "RuntimeDataScriptable", menuName = "Scriptables/RuntimeDataScriptable")]
    public class RuntimeDataScriptable : ScriptableObject
    {
        public ReactiveProperty<bool> canMove = new(true);
        public ReactiveProperty<bool> canClimb = new(true);
        public ReactiveProperty<bool> canPerformAction = new(true);
        
        public ReactiveProperty<bool> canUsePickaxe = new(true);
        public ReactiveProperty<bool> canUseWeapon = new(true);
    }
}