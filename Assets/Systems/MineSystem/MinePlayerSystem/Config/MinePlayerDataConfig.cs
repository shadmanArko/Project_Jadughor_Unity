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

        [Header("Default Movement Properties")]
        [Min(0f)] public float moveSpeed = 4f;
        [Min(0f)] public float climbSpeed = 3f;

        [Header("Default Action Properties")]
        [Min(0f)] public float miningSpeed = 1f;
        [Min(0f)] public float attackSpeed = 1f;

        [Header("Default Collectable Properties")]
        [Min(0f)] public float collectablePullRadius = 0.5f;

        [Header("Default Inventory Properties")]
        [Range(0, 36)] public int unlockedInventorySlots = 12;
    }
}
