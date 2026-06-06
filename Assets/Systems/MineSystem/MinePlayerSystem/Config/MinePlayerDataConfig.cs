using UnityEngine;

namespace Systems.MineSystem.MinePlayerSystem.Config
{
    [CreateAssetMenu(fileName = "MinePlayerDataConfig", menuName = "Config/MinePlayerDataConfig")]
    public class MinePlayerDataConfig : ScriptableObject
    {
        [Header("Basic Properties")] 
        public float health;
        public float maxHealth;
        
        public float stamina;
        public float maxStamina;

        [Header("Collectable Properties")]
        [Min(0f)] public float collectablePullRadius = 2.5f;
    }
}
