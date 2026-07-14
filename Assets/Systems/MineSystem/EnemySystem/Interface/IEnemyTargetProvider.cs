using Systems.MineSystem.Mine.Model;
using UnityEngine;

namespace Systems.MineSystem.EnemySystem.Interface
{
    public interface IEnemyTargetProvider
    {
        bool IsTargetAvailable { get; }
        bool IsCombatTargetAvailable { get; }
        GridPosition GridPosition { get; }
        Vector2 WorldPosition { get; }
        bool IsTargetCollider(Collider2D collider);
    }
}
