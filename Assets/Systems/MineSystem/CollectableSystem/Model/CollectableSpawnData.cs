using Systems.MineSystem.InventorySystem.Model;
using UnityEngine;

namespace Systems.MineSystem.CollectableSystem.Model
{
    public readonly struct CollectableSpawnData
    {
        public Item Item { get; }
        public Vector3 Position { get; }
        public Sprite Sprite { get; }

        public CollectableSpawnData(Item item, Vector3 position, Sprite sprite)
        {
            Item = item;
            Position = position;
            Sprite = sprite;
        }
    }
}
