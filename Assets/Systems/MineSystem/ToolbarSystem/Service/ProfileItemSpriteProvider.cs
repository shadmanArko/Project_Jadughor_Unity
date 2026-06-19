using Systems.MineSystem.CollectableSystem.Interface;
using Systems.MineSystem.InventorySystem.Model;
using Systems.MineSystem.Mine.Enum;
using Systems.MineSystem.ToolbarSystem.Profile;
using UnityEngine;

namespace Systems.MineSystem.ToolbarSystem.Service
{
    public sealed class ProfileItemSpriteProvider : ICollectableSpriteProvider
    {
        private readonly ItemActionProfileCatalog _profiles;

        public ProfileItemSpriteProvider(ItemActionProfileCatalog profiles)
        {
            _profiles = profiles;
        }

        public bool CanResolve(Item item)
        {
            return _profiles.TryGet(item, out var profile) &&
                   profile.IconSprite != null;
        }

        public Sprite Resolve(Item item, Region region, Site site)
        {
            return _profiles.TryGet(item, out var profile)
                ? profile.IconSprite
                : null;
        }
    }
}
