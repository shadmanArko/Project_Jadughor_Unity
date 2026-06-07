using UnityEngine;

namespace Systems.MineSystem.MinePlayerSystem.Config
{
    [CreateAssetMenu(fileName = "MinePlayerDataConfig", menuName = "Config/MinePlayerDataConfig")]
    public class MinePlayerDataConfig : ScriptableObject
    {
        [Header("Default Basic Properties")] 
        public float health;
        public float maxHealth;
        
        public float stamina;
        public float maxStamina;

        [Header("Default Collectable Properties")]
        [Min(0f)] public float collectablePullRadius = 0.5f;

        [Header("Default Inventory Properties")]
        [Range(0, 36)] public int unlockedInventorySlots = 12;
    }
}
