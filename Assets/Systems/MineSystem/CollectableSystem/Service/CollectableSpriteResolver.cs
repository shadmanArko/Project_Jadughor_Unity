using System.Collections.Generic;
using Systems.MineSystem.CollectableSystem.Interface;
using Systems.MineSystem.InventorySystem.Model;
using Systems.MineSystem.Mine.Enum;
using UnityEngine;

namespace Systems.MineSystem.CollectableSystem.Service
{
    public sealed class CollectableSpriteResolver
    {
        private readonly List<ICollectableSpriteProvider> _providers;

        public CollectableSpriteResolver(List<ICollectableSpriteProvider> providers)
        {
            _providers = providers;
        }

        public Sprite Resolve(Item item, Region region, Site site)
        {
            for (var i = 0; i < _providers.Count; i++)
            {
                var provider = _providers[i];
                if (!provider.CanResolve(item))
                    continue;

                return provider.Resolve(item, region, site);
            }

            return null;
        }
    }
}
