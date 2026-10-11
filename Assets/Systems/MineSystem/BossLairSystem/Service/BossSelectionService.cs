using Systems.MineSystem.BossLairSystem.Model;
using Systems.MineSystem.BossLairSystem.Scriptable;
using Systems.MineSystem.Mine.Enum;
using Random = System.Random;

namespace Systems.MineSystem.BossLairSystem.Service
{
    /// <summary>
    /// Decides whether the current mine contains a boss gate and, if so, which
    /// boss it belongs to. Pure selection only: placing the gate and building
    /// the lair are separate concerns.
    /// </summary>
    public sealed class BossSelectionService
    {
        private readonly BossSpawnTableScriptable _table;
        private readonly Random _random = new();

        public BossSelectionService(BossSpawnTableScriptable table) =>
            _table = table;

        public BossSelectionResult Select(Region region, Site site) =>
            Select(region, site, _random);

        private BossSelectionResult Select(Region region, Site site, Random random)
        {
            if (_table == null)
                return BossSelectionResult.None("No boss spawn table configured.");

            // Rolled before candidate selection so a boss-rich region still
            // yields boss-free runs at the authored rate.
            if (!Roll(random, _table.BossGateChancePercent))
                return BossSelectionResult.None(
                    "Run chance did not select a boss gate.");

            var candidates = _table.FindCandidates(region, site);
            if (candidates == null || candidates.Count == 0)
                return BossSelectionResult.None(
                    $"No boss candidates authored for {region}/{site}.");

            for (var i = 0; i < candidates.Count; i++)
            {
                var candidate = candidates[i];
                if (candidate?.profile == null)
                    continue;
                if (Roll(random, candidate.spawnChancePercent))
                    return BossSelectionResult.Selected(candidate.profile);
            }

            return BossSelectionResult.None(
                $"No candidate passed its spawn chance for {region}/{site}.");
        }

        private static bool Roll(Random random, float chancePercent)
        {
            if (chancePercent <= 0f)
                return false;
            if (chancePercent >= 100f)
                return true;
            return random.NextDouble() * 100d < chancePercent;
        }
    }
}
