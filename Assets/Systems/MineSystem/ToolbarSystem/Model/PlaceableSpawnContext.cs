using Systems.MineSystem.InventorySystem.Model;
using Systems.MineSystem.ToolbarSystem.Profile;
using UnityEngine;

namespace Systems.MineSystem.ToolbarSystem.Model
{
    public readonly struct PlaceableSpawnContext
    {
        public readonly string PlaceableId;
        public readonly string InstanceId;
        public readonly Item Item;
        public readonly PlaceableActionProfile Profile;
        public readonly Vector3Int CellPosition;
        public readonly Vector3 WorldPosition;

        public PlaceableSpawnContext(
            string placeableId,
            string instanceId,
            Item item,
            PlaceableActionProfile profile,
            Vector3Int cellPosition,
            Vector3 worldPosition)
        {
            PlaceableId = placeableId;
            InstanceId = instanceId;
            Item = item;
            Profile = profile;
            CellPosition = cellPosition;
            WorldPosition = worldPosition;
        }
    }
}
