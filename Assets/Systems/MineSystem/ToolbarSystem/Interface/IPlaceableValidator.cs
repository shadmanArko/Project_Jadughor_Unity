using Systems.MineSystem.InventorySystem.Model;
using Systems.MineSystem.ToolbarSystem.Profile;
using UnityEngine;

namespace Systems.MineSystem.ToolbarSystem.Interface
{
    public interface IPlaceableValidator
    {
        bool CanPlace(Vector3Int anchor, PlaceableActionProfile profile);
        bool TryReserve(
            Vector3Int anchor,
            PlaceableActionProfile profile,
            Item item,
            string instanceId);
        void Release(
            Vector3Int anchor,
            PlaceableActionProfile profile,
            string instanceId);
    }
}
