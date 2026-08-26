using System;
using Systems.MineSystem.BossLairSystem.Enum;
using UniRx;

namespace Systems.MineSystem.BossLairSystem.Model
{
    /// <summary>
    /// Logical state of the boss lair feature: whether a gate exists in the
    /// current mine, and where the player is in the visit lifecycle. The live
    /// lair instance itself is owned by <c>BossLairFactory</c>, so this model
    /// holds no Unity references.
    /// </summary>
    public sealed class BossLairModel : IDisposable
    {
        private readonly ReactiveProperty<BossLairState> _state =
            new(BossLairState.Idle);
        private bool _disposed;

        public IReadOnlyReactiveProperty<BossLairState> State => _state;

        /// <summary>Gate placed in the current mine, if any.</summary>
        public BossGatePlacement Gate { get; private set; }

        /// <summary>Resolved world geometry of the lair for the current visit.</summary>
        public BossLairPlacement Placement { get; private set; }

        public bool HasGate => Gate.IsValid;

        public bool IsIdle => _state.Value == BossLairState.Idle;

        /// <summary>
        /// True while a transition is running. Used to reject re-entrant entry
        /// or exit requests.
        /// </summary>
        public bool IsTransitioning =>
            _state.Value is BossLairState.Entering or BossLairState.Exiting;

        public void SetGate(BossGatePlacement gate) => Gate = gate;

        public void ClearGate() => Gate = default;

        public void SetPlacement(BossLairPlacement placement) =>
            Placement = placement;

        public void SetState(BossLairState state)
        {
            if (_disposed || _state.Value == state)
                return;
            _state.Value = state;
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            Gate = default;
            Placement = default;
            _state.Dispose();
        }
    }
}
