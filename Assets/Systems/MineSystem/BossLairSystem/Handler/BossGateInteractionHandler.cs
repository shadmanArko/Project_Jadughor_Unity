using Systems.MineSystem.BossLairSystem.Config;
using Systems.MineSystem.BossLairSystem.Controller;
using Systems.MineSystem.BossLairSystem.Enum;
using Systems.MineSystem.BossLairSystem.Model;
using Systems.MineSystem.BossLairSystem.Service;
using Systems.MineSystem.Mine.View;
using Systems.MineSystem.MinePlayerSystem.Interface;
using Systems.MineSystem.MinePlayerSystem.Model;
using Systems.MineSystem.MinePlayerSystem.Scriptable;
using UnityEngine;

namespace Systems.MineSystem.BossLairSystem.Handler
{
    /// <summary>
    /// Handles the Interact action for the boss gate in the mine and the exit
    /// anchor in the arena, resolving both off the player's current grid cell.
    /// </summary>
    /// <remarks>
    /// Priority comes from config and must stay above the elevator handler's
    /// 100. <c>PlayerInteractionService</c> stops at the first handler that
    /// returns true, and a placeable is allowed on any revealed broken cell, so a
    /// lower priority would let an elevator shaft dropped on the gate cell
    /// permanently shadow the gate.
    /// <para>
    /// The life-state check is this handler's own responsibility:
    /// <c>PlayerInteractionService.TryInteract</c> checks neither restriction
    /// flags nor life state, so without it a dead player could trigger a
    /// transition.
    /// </para>
    /// </remarks>
    public sealed class BossGateInteractionHandler : IPlayerInteractionHandler
    {
        private readonly BossLairController _controller;
        private readonly BossLairModel _model;
        private readonly BossLairFactory _factory;
        private readonly RuntimeDataScriptable _runtime;
        private readonly MineView _mineView;
        private readonly BossLairConfig _config;

        public BossGateInteractionHandler(
            BossLairController controller,
            BossLairModel model,
            BossLairFactory factory,
            RuntimeDataScriptable runtime,
            MineView mineView,
            BossLairConfig config)
        {
            _controller = controller;
            _model = model;
            _factory = factory;
            _runtime = runtime;
            _mineView = mineView;
            _config = config;
        }

        public int Priority => _config.GateInteractionPriority;

        public bool TryInteract()
        {
            if (_runtime.lifeState.Value != PlayerLifeState.Alive ||
                _model.IsTransitioning)
                return false;

            return _model.State.Value switch
            {
                BossLairState.Idle => TryEnter(),
                BossLairState.Active => TryExit(),
                _ => false
            };
        }

        private bool TryEnter()
        {
            if (!_model.HasGate)
                return false;
            var playerCell = _mineView.grid.WorldToCell(
                _runtime.worldPosition.Value);
            if (playerCell != _model.Gate.Cell)
                return false;
            _controller.RequestEnter();
            return true;
        }

        private bool TryExit()
        {
            var lair = _factory.Active;
            if (lair == null || lair.exitAnchor == null)
                return false;
            var playerCell = lair.grid.WorldToCell(_runtime.worldPosition.Value);
            var exitCell = lair.grid.WorldToCell(lair.exitAnchor.position);
            if (playerCell != exitCell)
                return false;
            _controller.RequestExit();
            return true;
        }
    }
}
