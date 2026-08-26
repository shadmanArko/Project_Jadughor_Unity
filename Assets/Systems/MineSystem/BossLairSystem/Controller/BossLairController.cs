using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Systems.MineSystem.BossLairSystem.Config;
using Systems.MineSystem.BossLairSystem.Enum;
using Systems.MineSystem.BossLairSystem.Model;
using Systems.MineSystem.BossLairSystem.Scriptable;
using Systems.MineSystem.BossLairSystem.Service;
using Systems.MineSystem.BossLairSystem.Signal;
using Systems.MineSystem.Mine.Signal;
using Systems.MineSystem.MinePlayerSystem.Model;
using Systems.MineSystem.MinePlayerSystem.Scriptable;
using Systems.MineSystem.PauseSystem.Interface;
using Systems.MineSystem.PauseSystem.Signal;
using Systems.Utilities.EventBus;
using UniRx;
using UnityEngine;
using Zenject;

namespace Systems.MineSystem.BossLairSystem.Controller
{
    /// <summary>
    /// Owns the boss lair lifecycle: rolls for a gate when a mine is generated,
    /// builds the arena in the same pass, runs entry and exit, and guarantees the
    /// game is left in a valid state on death or teardown.
    /// </summary>
    /// <remarks>
    /// No tick is registered. Nothing in the lair needs per-frame work yet; the
    /// boss state machine will add one when it lands, and it should be bound with
    /// an explicit execution order because <c>PlayerModel.FixedTick</c> writes the
    /// player world position that boss AI will read.
    /// </remarks>
    public sealed class BossLairController :
        IPausable,
        IInitializable,
        IDisposable
    {
        private readonly BossLairModel _model;
        private readonly BossGateSpawnService _gateSpawn;
        private readonly BossGatePlacementService _gatePlacement;
        private readonly BossLairBuildService _build;
        private readonly BossLairEntryService _entry;
        private readonly BossLairExitService _exit;
        private readonly BossLairPauseService _pause;
        private readonly BossLairConfig _config;
        private readonly BossSpawnTableScriptable _spawnTable;
        private readonly RuntimeDataScriptable _runtime;
        private readonly CompositeDisposable _subscriptions = new();
        private CancellationTokenSource _lifetime;
        private CancellationTokenSource _activeTransition;
        private bool _isAffectedByPause = true;
        private bool _disposed;

        public BossLairController(
            BossLairModel model,
            BossGateSpawnService gateSpawn,
            BossGatePlacementService gatePlacement,
            BossLairBuildService build,
            BossLairEntryService entry,
            BossLairExitService exit,
            BossLairPauseService pause,
            BossLairConfig config,
            BossSpawnTableScriptable spawnTable,
            RuntimeDataScriptable runtime)
        {
            _model = model;
            _gateSpawn = gateSpawn;
            _gatePlacement = gatePlacement;
            _build = build;
            _entry = entry;
            _exit = exit;
            _pause = pause;
            _config = config;
            _spawnTable = spawnTable;
            _runtime = runtime;
        }

        public bool IsAffectedByPause
        {
            get => _isAffectedByPause;
            set
            {
                if (_isAffectedByPause == value)
                    return;
                _isAffectedByPause = value;
                GlobalEventBus.Fire(new PausableAffectationChangedSignal(this));
            }
        }

        public void Initialize()
        {
            _lifetime = new CancellationTokenSource();

            // Reported, not thrown: a misconfigured lair must never stop the mine
            // from being playable. Validating the spawn table here walks the whole
            // authored chain - profile, gate prefab, boss config, arena config -
            // so a half-set-up boss is reported at startup rather than failing
            // quietly on the run where it finally gets rolled.
            if (_config != null && !_config.Validate(out var configError))
                Debug.LogError($"[BossLair] {configError}");
            if (_spawnTable != null && !_spawnTable.Validate(out var tableError))
                Debug.LogError($"[BossLair] {tableError}");

            GlobalEventBus.OnSignal<MineGeneratedSignal>()
                .Subscribe(signal => HandleMineGenerated(signal))
                .AddTo(_subscriptions);

            // Death is a dead end elsewhere in the project: PlayerDeathService
            // fires no signal and there is no respawn flow. Left alone, dying in
            // the arena would strand the player with no way back.
            _runtime.lifeState
                .Where(state => state == PlayerLifeState.Dead)
                .Subscribe(_ => HandlePlayerDeath())
                .AddTo(_subscriptions);

            GlobalEventBus.Fire(new PausableRegisteredSignal(this));
        }

        public void OnPause() => _pause.Pause();
        public void OnUnpause() => _pause.Resume();

        /// <summary>
        /// Rolls for a boss gate and, when one is placed, builds the arena as part
        /// of the same generation pass. Runs without a boss build nothing.
        /// </summary>
        private void HandleMineGenerated(MineGeneratedSignal signal)
        {
            if (_disposed)
                return;

            _model.SetState(BossLairState.Idle);
            _build.Teardown();

            if (!_gateSpawn.TrySpawnForMine(signal.MineData))
                return;
            if (_build.Build(_model.Gate.Profile))
                return;

            // A gate the player could open onto a missing arena would be worse
            // than no gate at all.
            Debug.LogWarning(
                "[BossLair] Arena build failed, so the gate was removed.");
            _gatePlacement.Clear();
            _model.ClearGate();
        }

        public void RequestEnter()
        {
            if (_disposed || !_model.IsIdle || !_model.HasGate)
                return;
            EnterAsync().Forget(Debug.LogException);
        }

        public void RequestExit()
        {
            if (_disposed || _model.State.Value != BossLairState.Active)
                return;
            ExitAsync(bossDefeated: false, playerDied: false)
                .Forget(Debug.LogException);
        }

        private async UniTask EnterAsync()
        {
            var profile = _model.Gate.Profile;
            _model.SetState(BossLairState.Entering);
            var token = BeginTransition();

            var entered = await _entry.ExecuteAsync(token);
            EndTransition();

            if (!entered)
            {
                _model.SetState(BossLairState.Idle);
                return;
            }

            _model.SetState(BossLairState.Active);
            GlobalEventBus.Fire(new BossLairEnteredSignal(profile));
        }

        private async UniTask ExitAsync(bool bossDefeated, bool playerDied)
        {
            var profile = _model.Gate.Profile;
            _model.SetState(BossLairState.Exiting);
            var token = BeginTransition();

            await _exit.ExecuteAsync(token);

            EndTransition();
            _model.SetState(BossLairState.Idle);
            GlobalEventBus.Fire(
                new BossLairExitedSignal(profile, bossDefeated, playerDied));
        }

        /// <summary>
        /// Unwinds a visit when the player dies. Runs synchronously so the state
        /// cannot be left half-restored while a death animation plays.
        /// </summary>
        private void HandlePlayerDeath()
        {
            if (_disposed || _model.IsIdle)
                return;

            var profile = _model.Gate.Profile;
            _activeTransition?.Cancel();
            _exit.ReturnToMine();
            EndTransition();
            _model.SetState(BossLairState.Idle);
            GlobalEventBus.Fire(
                new BossLairExitedSignal(profile, false, playerDied: true));
        }

        private CancellationToken BeginTransition()
        {
            _activeTransition?.Dispose();
            _activeTransition =
                CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
            return _activeTransition.Token;
        }

        private void EndTransition()
        {
            _activeTransition?.Dispose();
            _activeTransition = null;
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            GlobalEventBus.Fire(new PausableUnregisteredSignal(this));

            if (_lifetime != null && !_lifetime.IsCancellationRequested)
                _lifetime.Cancel();
            _activeTransition?.Cancel();
            _activeTransition?.Dispose();
            _activeTransition = null;
            _lifetime?.Dispose();
            _lifetime = null;

            // Only destroy owned objects. Restoring the player and camera is
            // deliberately skipped: disposal happens during container teardown,
            // where those components may already be destroyed. The camera service
            // handles its own restore for the live-teardown case.
            try
            {
                _build.Teardown();
                _gatePlacement.Clear();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
            _subscriptions.Dispose();
        }
    }
}
