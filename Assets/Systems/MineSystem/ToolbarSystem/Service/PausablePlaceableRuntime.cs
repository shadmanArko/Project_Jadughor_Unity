using Systems.MineSystem.PauseSystem.Interface;
using Systems.MineSystem.PauseSystem.Signal;
using Systems.MineSystem.ToolbarSystem.Interface;
using Systems.MineSystem.ToolbarSystem.Model;
using Systems.Utilities.EventBus;
using UnityEngine;

namespace Systems.MineSystem.ToolbarSystem.Service
{
    public abstract class PausablePlaceableRuntime : MonoBehaviour, IPausable
    {
        private readonly PlaceablePauseStateData _pauseState = new();
        private bool _isAffectedByPause = true;
        public abstract IPlaceableDamageView DamageView { get; }

        public bool IsAffectedByPause
        {
            get => _isAffectedByPause;
            set
            {
                if (_isAffectedByPause == value) return;
                _isAffectedByPause = value;
                GlobalEventBus.Fire(new PausableAffectationChangedSignal(this));
            }
        }

        public virtual void OnPause() => _pauseState.Capture(transform, DamageView);
        public virtual void OnUnpause() => _pauseState.Restore(DamageView);
        protected void ClearPauseState() => _pauseState.Clear();
    }
}
