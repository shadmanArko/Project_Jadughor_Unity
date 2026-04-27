using System;
using Systems.MineSystem.Mine.Enum;
using UnityEngine;

namespace Systems.MineSystem.MineGenerationSystem.Model
{
    [Serializable]
    public class MineTile
    {
        public BrokenEdges brokenEdge;
        public Sprite tileSprite;
    }
}