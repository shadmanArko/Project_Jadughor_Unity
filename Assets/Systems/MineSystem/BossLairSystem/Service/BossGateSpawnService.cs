using Systems.MineSystem.BossLairSystem.Model;
using Systems.MineSystem.BossLairSystem.Signal;
using Systems.MineSystem.Mine.Model;
using Systems.MineSystem.MinePlayerSystem.Scriptable;
using Systems.Utilities.EventBus;
using UnityEngine;

namespace Systems.MineSystem.BossLairSystem.Service
{
    /// <summary>
    /// Decides whether the freshly generated mine gets a boss gate, and places
    /// it if so. Runs once per generated mine.
    /// </summary>
    /// <remarks>
    /// Region and site come from <see cref="MinePlayerScriptable"/>, which is the
    /// live source for the current run. The equivalent fields on
    /// <c>MineGenerationConfig</c> are commented out.
    /// </remarks>
    public sealed class BossGateSpawnService
    {
        private readonly BossSelectionService _selection;
        private readonly BossGatePlacementService _placement;
        private readonly BossLairModel _model;
        private readonly MinePlayerScriptable _player;

        public BossGateSpawnService(
            BossSelectionService selection,
            BossGatePlacementService placement,
            BossLairModel model,
            MinePlayerScriptable player)
        {
            _selection = selection;
            _placement = placement;
            _model = model;
            _player = player;
        }

        public bool TrySpawnForMine(MineData mineData)
        {
            _placement.Clear();
            _model.ClearGate();

            if (mineData == null)
                return false;

            var selection = _selection.Select(_player.region, _player.site);
            if (!selection.HasBoss)
            {
                // Not an error: most runs are intended to have no boss.
                Debug.Log($"[BossLair] No boss gate this run: {selection.Reason}");
                return false;
            }

            if (!_placement.TryPlace(mineData, selection.Profile, out var placement))
            {
                Debug.LogWarning(
                    $"[BossLair] {selection.Profile.DisplayName} was selected but " +
                    "no cave cell could host its gate; this run has no boss.");
                return false;
            }

            _model.SetGate(placement);
            GlobalEventBus.Fire(
                new BossGatePlacedSignal(placement.Cell, placement.Profile));
            Debug.Log(
                $"[BossLair] Placed {selection.Profile.DisplayName} gate at " +
                $"cell {placement.Cell}.");
            return true;
        }
    }
}
