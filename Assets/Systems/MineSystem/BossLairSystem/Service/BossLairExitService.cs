using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Systems.MineSystem.BossLairSystem.Config;
using Systems.MineSystem.BossLairSystem.Model;
using Systems.MineSystem.Mine.View;
using Systems.MineSystem.MinePlayerSystem.Service;
using UnityEngine;

namespace Systems.MineSystem.BossLairSystem.Service
{
    /// <summary>
    /// Returns the player from the arena to the gate cell in the mine.
    /// </summary>
    /// <remarks>
    /// The arena is <b>not</b> destroyed here — it is built during mine generation
    /// and lives until the mine regenerates, so leaving and re-entering costs
    /// nothing. Exit only puts the arena back to sleep and restores the mine's
    /// camera.
    /// <para>
    /// Written so it is safe on any exit path, including a failed entry or a
    /// player death, and safe to call twice.
    /// </para>
    /// </remarks>
    public sealed class BossLairExitService
    {
        private readonly PlayerTransitionService _player;
        private readonly BossLairCameraService _camera;
        private readonly BossLairFactory _factory;
        private readonly BossLairPauseService _pause;
        private readonly BossLairModel _model;
        private readonly BossLairConfig _config;
        private readonly MineView _mineView;

        public BossLairExitService(
            PlayerTransitionService player,
            BossLairCameraService camera,
            BossLairFactory factory,
            BossLairPauseService pause,
            BossLairModel model,
            BossLairConfig config,
            MineView mineView)
        {
            _player = player;
            _camera = camera;
            _factory = factory;
            _pause = pause;
            _model = model;
            _config = config;
            _mineView = mineView;
        }

        public async UniTask ExecuteAsync(CancellationToken cancellationToken)
        {
            _player.SetManualControlsEnabled(false);
            try
            {
                await _pause.WaitAsync();
                ReturnToMine();

                if (_config.ArenaEntryDuration > 0f)
                    await UniTask.Delay(
                        TimeSpan.FromSeconds(_config.ArenaEntryDuration),
                        cancellationToken: cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // The player is already back in the mine; only the settle delay
                // was interrupted.
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
            finally
            {
                _player.ClearForcedAnimation();
                _player.SetManualControlsEnabled(true);
            }
        }

        /// <summary>
        /// The synchronous part of the exit. Kept separate so paths that cannot
        /// await, such as reacting to player death, can still restore a valid
        /// state.
        /// </summary>
        public void ReturnToMine()
        {
            if (_model.HasGate)
            {
                var worldPosition =
                    _mineView.grid.GetCellCenterWorld(_model.Gate.Cell);
                _player.Teleport(worldPosition);
            }

            // Camera after the teleport, so it never spends a frame following the
            // player while they are still inside the arena.
            _camera.ExitLair(_player.PlayerTransform);
            _factory.Active?.SetArenaActive(false);
        }
    }
}
