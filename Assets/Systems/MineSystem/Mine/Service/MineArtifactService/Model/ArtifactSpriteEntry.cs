using System;
using UnityEngine;

namespace Systems.MineSystem.Mine.Service.MineArtifactService.Test
{
    [Serializable]
    public sealed class ArtifactSpriteEntry
    {
        public string definitionId;
        public Sprite worldSprite;
        public Sprite inventorySprite;
        public Sprite detailSprite;

        public Sprite GetWorldSprite()
        {
            return worldSprite != null
                ? worldSprite
                : inventorySprite != null
                    ? inventorySprite
                    : detailSprite;
        }
    }
}