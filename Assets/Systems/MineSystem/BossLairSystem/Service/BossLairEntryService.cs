using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Systems.MineSystem.ActorSystem.Interface;
using Systems.MineSystem.BossLairSystem.Config;
using Systems.MineSystem.BossLairSystem.Model;
using Systems.MineSystem.BossLairSystem.View;
using Systems.MineSystem.EnemySystem.Mob.HedgehogBoss.Model;
using Systems.MineSystem.MinePlayerSystem.Model;
using Systems.MineSystem.MinePlayerSystem.Service;
using Systems.MineSystem.MinePlayerSystem.SubSystem.PlayerAnimationSubSystem.Enum;
using UnityEngine;

namespace Systems.MineSystem.BossLairSystem.Service
{
    /// <summary>
    /// Takes the player from a gate in the mine into the arena: walk to the gate,
    /// wake the arena, reframe the camera, place the player, hand control back.
    /// </summary>
    /// <remarks>
    /// The arena already exists — it is built during mine generation — so this is
    /// presentation and handover only, with no build cost at the moment of entry.
    /// <para>
    /// The camera cuts rather than pans. The lair sits well below the mine by
    /// design, so a pan across that gap would be a long slide over empty space. A
    /// screen fade would be the natural cover, but the project has no fade widget
    /// yet, so the cut is deliberate and this seam is where one should later go.
    /// </para>
    /// <para>
    /// Every beat is separated by <c>_pause.WaitAsync()</c> so modal UI suspends
    /// the transition instead of racing it.
    /// </para>
    /// </remarks>
    public sealed class BossLairEntryService
    {
        private readonly PlayerTransitionService _player;
        private readonly BossLairCameraService _camera;
        private readonly BossLairFactory _factory;
        private readonly BossLairBossFactory _bossFactory;
        private readonly BossGatePlacementService _gatePlacement;
        private readonly BossLairPauseService _pause;
        private readonly BossLairModel _model;
        private readonly BossLairConfig _config;

        public BossLairEntryService(
            PlayerTransitionService player,
            BossLairCameraService camera,
            BossLairFactory factory,
            BossLairBossFactory bossFactory,
            BossGatePlacementService gatePlacement,
            BossLairPauseService pause,
            BossLairModel model,
            BossLairConfig config)
        {
            _player = player;
            _camera = camera;
            _factory = factory;
            _bossFactory = bossFactory;
            _gatePlacement = gatePlacement;
            _pause = pause;
            _model = model;
            _config = config;
        }

        public async UniTask<bool> ExecuteAsync(CancellationToken cancellationToken)
        {
            var view = _factory.Active;
            var placement = _model.Placement;
            var lairConfig = _model.Gate.Profile?.ProceduralLairConfig;
            if (view == null || !placement.IsValid || lairConfig == null)
            {
                Debug.LogWarning(
                    "[BossLair] Entry requested but no arena is built for this run.");
                return false;
            }

            if (view.playerSpawnPoint == null)
            {
                Debug.LogError(
                    "[BossLair] Entry requested without a PlayerSpawn anchor.");
                return false;
            }

            // The authored PlayerSpawn anchor is the authoritative position.
            // Do not apply a ground probe or offset here: designers use this
            // Transform to control the exact entry point.
            var spawnPosition = (Vector2)view.playerSpawnPoint.position;

            _player.SetManualControlsEnabled(false);
            try
            {
                await _pause.WaitAsync();
                await WalkToGateAsync(cancellationToken);

                await _pause.WaitAsync();
                view.SetArenaActive(true);
                _camera.EnterLair(
                    view,
                    placement,
                    lairConfig.LairAssetsPPU,
                    _player.PlayerTransform);
                _player.Teleport(spawnPosition);

                await _pause.WaitAsync();
                if (_config.ArenaEntryDuration > 0f)
                    await UniTask.Delay(
                        TimeSpan.FromSeconds(_config.ArenaEntryDuration),
                        cancellationToken: cancellationToken);

                await _pause.WaitAsync();
                await BossIntroAsync(cancellationToken);

                _player.SetManualControlsEnabled(true);
                return true;
            }
            catch (OperationCanceledException)
            {
                RollBack(view);
                return false;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                RollBack(view);
                return false;
            }
            finally
            {
                _player.ClearForcedAnimation();
            }
        }

        /// <summary>
        /// Has the boss step toward the player and roar, if this lair has a
        /// boss instance. No-ops safely for a lair variant with no boss, so
        /// entry behaves exactly as before when there is nothing to animate.
        /// </summary>
        private async UniTask BossIntroAsync(CancellationToken cancellationToken)
        {
            IActor boss = _bossFactory.Active;
            if (boss == null)
                return;

            var facesLeft = _player.Position.x < boss.Position.x;
            var direction = facesLeft ? Vector2.left : Vector2.right;
            var stepTarget = boss.Position +
                direction * _config.BossIntroStepDistance;

            boss.PlayAnimation(HedgehogBossAnimationId.Move, facesLeft);
            await boss.MoveToAsync(
                stepTarget,
                _config.BossIntroWalkDuration,
                _config.PlayerMovementEase,
                cancellationToken);

            boss.PlayAnimation(HedgehogBossAnimationId.Roar, facesLeft);
            if (_config.BossIntroRoarDuration > 0f)
                await UniTask.Delay(
                    TimeSpan.FromSeconds(_config.BossIntroRoarDuration),
                    cancellationToken: cancellationToken);

            boss.ClearAnimation();
        }

        private async UniTask WalkToGateAsync(CancellationToken cancellationToken)
        {
            var gate = _gatePlacement.ActiveGate;
            if (gate == null || _config.GateApproachDuration <= 0f)
                return;

            var target = gate.ApproachPosition;
            var facing = target.x >= _player.Position.x
                ? PlayerFacingDirection.Right
                : PlayerFacingDirection.Left;
            _player.PlayForcedAnimation(PlayerAnimationId.Move, facing);
            await _player.AutoMoveAsync(
                target,
                _config.GateApproachDuration,
                _config.PlayerMovementEase,
                cancellationToken);
            _player.ClearForcedAnimation();
        }

        /// <summary>
        /// Undoes a partial entry so a cancelled or failed transition cannot leave
        /// the camera zoomed into the arena with the player still in the mine. The
        /// arena itself is not destroyed — it lives until the mine regenerates.
        /// </summary>
        private void RollBack(BossLairView view)
        {
            view.SetArenaActive(false);
            _camera.ExitLair(_player.PlayerTransform);
            _player.SetManualControlsEnabled(true);
            _bossFactory.Active?.ClearAnimation();
        }
    }
}
