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

        public IObservable<float> DamageRequested => _damageRequested;

        public void ApplyDamage(float amount)
        {
            _damageRequested.OnNext(amount);
        }

        public bool ConvertToItem()
        {
            return _convertToItem?.Invoke() == true;
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
