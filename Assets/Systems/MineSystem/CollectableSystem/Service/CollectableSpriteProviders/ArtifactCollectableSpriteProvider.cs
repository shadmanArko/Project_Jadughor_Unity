using System;
using Systems.MineSystem.CollectableSystem.Interface;
using Systems.MineSystem.InventorySystem.Model;
using Systems.MineSystem.Mine.Enum;
using Systems.MineSystem.Mine.Service.MineArtifactService.Test;
using UnityEngine;

namespace Systems.MineSystem.CollectableSystem.Service.CollectableSpriteProviders
{
    [Serializable]
    public sealed class ArtifactCollectableSpriteProvider : ICollectableSpriteProvider
    {
        private readonly ArtifactSpriteScriptable _sprites;

        public ArtifactCollectableSpriteProvider(ArtifactSpriteScriptable sprites)
        {
            _sprites = sprites;
        }

        public bool CanResolve(Item item) => item is Artifact;

        public Sprite Resolve(Item item, Region region, Site site)
        {
            return _sprites.GetInventorySprite(
                ((Artifact)item).DefinitionId,
                region,
                site);
        }
    }
}