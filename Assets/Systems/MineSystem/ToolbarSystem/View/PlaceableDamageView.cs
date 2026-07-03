using System;
using Systems.MineSystem.ToolbarSystem.Interface;
using UniRx;
using UnityEngine;

namespace Systems.MineSystem.ToolbarSystem.View
{
    public abstract class PlaceableDamageView :
        MonoBehaviour,
        IPlaceableDamageView
    {
        private readonly Subject<float> _damageRequested = new();
        private Func<bool> _convertToItem;
        private bool _damageEnabled = true;

        public IObservable<float> DamageRequested => _damageRequested;
        public bool DamageEnabled => _damageEnabled;

        public void ApplyDamage(float amount)
        {
            if (_damageEnabled)
                _damageRequested.OnNext(amount);
        }

        public void SetDamageEnabled(bool enabled) => _damageEnabled = enabled;

        public bool ConvertToItem()
        {
            return _damageEnabled && _convertToItem?.Invoke() == true;
        }

        public void ConfigureItemization(Func<bool> convertToItem)
        {
            _convertToItem = convertToItem;
        }

        public void ClearItemization()
        {
            _convertToItem = null;
        }

        protected virtual void OnDestroy()
        {
            ClearItemization();
            _damageRequested.OnCompleted();
            _damageRequested.Dispose();
        }
    }
}
