using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

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
        [Min(0f)] public float moveSpeed = 0.25f;
        [Min(0f)] public float climbSpeed = 0.5f;
        [Min(0f)] public float normalGravityScale = 0.5f;

        [Header("Ground Detection")]
        public LayerMask wallLayerMask = 1 << 7;
        [Min(0.001f)] public float groundProbeThickness = 0.04f;
        [Min(0.001f)] public float groundProbeDistance = 0.06f;
        [Min(0f)] public float groundProbeWidthInset = 0.04f;
        [Range(0f, 1f)] public float minimumGroundNormalY = 0.5f;

        [Header("Fall Damage")]
        [Tooltip("Number of mine cells the player may descend safely. The fall animation and damage eligibility begin only after crossing this count.")]
        [FormerlySerializedAs("safeFallDistance")]
        [Min(0f)] public float safeFallCells = 2f;
        public List<PlayerFallDamageThreshold> fallDamageThresholds = new();

        [Header("Default Action Properties")]
        [Min(0f)] public float miningSpeed = 1f;
        [Min(0f)] public float attackSpeed = 1f;

        [Header("Default Collectable Properties")]
        [Min(0f)] public float collectablePullRadius = 0.5f;

        [Header("Default Inventory Properties")]
        [Range(0, 36)] public int unlockedInventorySlots = 12;

        private void OnValidate()
        {
            fallDamageThresholds.Sort((left, right) =>
                left.minimumCells.CompareTo(right.minimumCells));
        }
    }
}
