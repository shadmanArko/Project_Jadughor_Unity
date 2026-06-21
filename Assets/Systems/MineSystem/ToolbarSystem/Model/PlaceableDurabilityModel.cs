using System;
using Systems.MineSystem.ToolbarSystem.Interface;
using UniRx;
using UnityEngine;

namespace Systems.MineSystem.ToolbarSystem.Model
{
    public sealed class PlaceableDurabilityModel : IDisposable
    {
        private readonly float _maxHealth;
        private readonly IPlaceableDamageView _view;
        private readonly Action _onConverted;
        private readonly CompositeDisposable _disposables = new();
        private float _health;
        private bool _processingLethalDamage;

        public PlaceableDurabilityModel(
            IPlaceableDamageView view,
            float maxHealth,
            Action onConverted)
        {
            _view = view;
            _maxHealth = Mathf.Max(1f, maxHealth);
            _health = _maxHealth;
            _onConverted = onConverted;

            view.DamageRequested
                .Subscribe(ApplyDamage)
                .AddTo(_disposables);
        }

        private void ApplyDamage(float amount)
        {
            if (amount <= 0f || _processingLethalDamage)
                return;

            _health = Mathf.Max(0f, _health - amount);
            if (_health > 0f)
                return;

            _processingLethalDamage = true;
            if (!_view.ConvertToItem())
            {
                _health = 1f;
                _processingLethalDamage = false;
                return;
            }

            _onConverted?.Invoke();
        }

        public void Dispose()
        {
            _disposables.Dispose();
        }
    }
}
