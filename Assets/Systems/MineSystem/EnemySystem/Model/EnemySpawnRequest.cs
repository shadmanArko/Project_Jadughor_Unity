using System.Collections.Generic;
using Systems.MineSystem.EnemySystem.Config;
using Systems.MineSystem.EnemySystem.Enum;
using Systems.MineSystem.Mine.Model;

namespace Systems.MineSystem.EnemySystem.Model
{
    public readonly struct EnemySpawnRequest
    {
        public readonly EnemyConfigScriptable Config;
        public readonly GridPosition? PreferredPosition;
        public readonly IReadOnlyCollection<GridPosition> OccupiedPositions;
        public readonly EnemySpawnVisibilityRule VisibilityRule;
        public readonly int OutsideCameraMarginInTiles;

        public EnemySpawnRequest(
            EnemyConfigScriptable config,
            GridPosition? preferredPosition = null,
            IReadOnlyCollection<GridPosition> occupiedPositions = null,
            EnemySpawnVisibilityRule visibilityRule =
                EnemySpawnVisibilityRule.Any,
            int outsideCameraMarginInTiles = 0)
        {
            Config = config;
            PreferredPosition = preferredPosition;
            OccupiedPositions = occupiedPositions;
            VisibilityRule = visibilityRule;
            OutsideCameraMarginInTiles = outsideCameraMarginInTiles;
        }

        public EnemySpawnRequest WithOccupiedPositions(
            IReadOnlyCollection<GridPosition> positions)
        {
            return new EnemySpawnRequest(
                Config,
                PreferredPosition,
                positions,
                VisibilityRule,
                OutsideCameraMarginInTiles);
        }
    }
}
