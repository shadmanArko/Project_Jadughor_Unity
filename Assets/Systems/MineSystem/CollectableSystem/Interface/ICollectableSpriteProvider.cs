using Systems.MineSystem.InventorySystem.Model;
using Systems.MineSystem.Mine.Enum;
using UnityEngine;

namespace Systems.MineSystem.CollectableSystem.Interface
{
    public interface ICollectableSpriteProvider
    {
        bool CanResolve(Item item);
        Sprite Resolve(Item item, Region region, Site site);
    }
}
