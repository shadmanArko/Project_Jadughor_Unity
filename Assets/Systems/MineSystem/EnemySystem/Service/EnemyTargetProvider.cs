using Systems.MineSystem.EnemySystem.Interface;
using Systems.MineSystem.Mine.Model;
using Systems.MineSystem.Mine.View;
using Systems.MineSystem.MinePlayerSystem.Model;
using Systems.MineSystem.MinePlayerSystem.Scriptable;
using UnityEngine;

namespace Systems.MineSystem.EnemySystem.Service
{
    public sealed class EnemyTargetProvider : IEnemyTargetProvider
    {
        private readonly RuntimeDataScriptable _runtime;
        private readonly MineView _mineView;

        public EnemyTargetProvider(
            RuntimeDataScriptable runtime,
            MineView mineView)
        {
            _runtime = runtime;
            _mineView = mineView;
        }

        public bool IsTargetAvailable =>
            _runtime != null && _runtime.isSpawned.Value &&
            _runtime.lifeState.Value == PlayerLifeState.Alive;

        public Vector2 WorldPosition => _runtime.worldPosition.Value;

        public GridPosition GridPosition
        {
            get
            {
                if (_mineView?.grid == null)
                    return default;
                var cell = _mineView.grid.WorldToCell(_runtime.worldPosition.Value);
                return new GridPosition(cell.x, cell.y);
            }
        }
    }
}
