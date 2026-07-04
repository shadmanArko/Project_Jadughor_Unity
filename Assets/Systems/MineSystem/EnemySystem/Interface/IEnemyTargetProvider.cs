using Systems.MineSystem.Mine.Model;
using UnityEngine;

namespace Systems.MineSystem.EnemySystem.Interface
{
    public interface IEnemyTargetProvider
    {
        bool IsTargetAvailable { get; }
        GridPosition GridPosition { get; }
        Vector2 WorldPosition { get; }
    }
}
