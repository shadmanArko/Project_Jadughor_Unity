using Systems.MineSystem.Mine.Model;
using UnityEngine;

namespace Systems.MineSystem.EnemySystem.Interface
{
    public interface IEnemyPlacementValidator
    {
        bool TryGetPlacement(
            Collider2D terrainCollider,
            GridPosition position,
            out Vector2 worldPosition);

        bool IsPlacementClear(
            Collider2D terrainCollider,
            Vector2 worldPosition);

        bool IsCurrentPlacementClear(Collider2D terrainCollider);

        GridPosition WorldToGrid(Vector2 worldPosition);
    }
}
