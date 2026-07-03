using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Systems.MineSystem.Mine.Config
{
    [CreateAssetMenu(fileName = "VineConfig", menuName = "Config/VineConfig")]
    public sealed class VineConfig : ScriptableObject
    {
        [Min(1)] [SerializeField] private int minGroupLength = 4;
        [Min(1)] [SerializeField] private int maxGroupLength = 12;
        [Min(0)] [SerializeField] private int minGroupsPerMine = 8;
        [Min(0)] [SerializeField] private int maxGroupsPerMine = 16;
        [SerializeField] private List<VineTypeConfig> vineTypes = new()
        {
            new VineTypeConfig("defaultVine", 1.5f, 1)
        };

        public int MinGroupLength => Mathf.Max(1, minGroupLength);
        public int MaxGroupLength => Mathf.Max(MinGroupLength, maxGroupLength);
        public int MinGroupsPerMine => Mathf.Max(0, minGroupsPerMine);
        public int MaxGroupsPerMine => Mathf.Max(MinGroupsPerMine, maxGroupsPerMine);
        public IReadOnlyList<VineTypeConfig> VineTypes => vineTypes;

        public float GetClimbSpeedMultiplier(string id)
        {
            var vineType = vineTypes?.FirstOrDefault(type => type != null && type.Id == id);
            return vineType != null ? vineType.ClimbSpeedMultiplier : 1f;
        }
    }

    [Serializable]
    public sealed class VineTypeConfig
    {
        [SerializeField] private string id;
        [SerializeField] private float climbSpeedMultiplier = 1.5f;
        [Min(0)] [SerializeField] private int generationWeight = 1;

        public VineTypeConfig(string id, float climbSpeedMultiplier, int generationWeight)
        {
            this.id = id;
            this.climbSpeedMultiplier = climbSpeedMultiplier;
            this.generationWeight = generationWeight;
        }

        public string Id => id;
        public float ClimbSpeedMultiplier => climbSpeedMultiplier;
        public int GenerationWeight => Mathf.Max(0, generationWeight);
    }
}
