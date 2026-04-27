using System.Collections.Generic;
using Systems.MineSystem.Mine.Enum;
using UnityEngine;

namespace Systems.MineSystem.Mine.Config
{
    [CreateAssetMenu(fileName = "MineGenerationConfig", menuName = "Configs/MineGenerationConfig")]
    public class MineGenerationConfig : ScriptableObject
    {
        [Header("Mine Data")] public int mineSizeX;
        public int mineSizeY;

        [Header("Cell Data")] 
        public int cellSize;
        public int hardCellHitPoint;
        public int regularCellHitPoint;

        [Header("Boss Cave Data")] public bool hasBossCave;
        public int bossCaveSizeX;
        public int bossCaveSizeY;

        [Header("Cave Data")] 
        public int numberOfMaxCaves;
        public int caveMinSizeX;
        public int caveMinSizeY;
        public int caveMaxSizeX;
        public int caveMaxSizeY;
        public int minDistanceBetweenCaves;

        [Header("Stalagmite Stalactite Data")] 
        public int stalagmiteCount;
        public int stalactiteCount;

        [Header("Location")] 
        public Region region;
        public Site site;
        
        [Header("Artifact Data")]
        public int artifactCount;

        [Header("Resource Data")] 
        public int resourceCount;
        public List<ResourceVariant> resourceVariants;
    }
}