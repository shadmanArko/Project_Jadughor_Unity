using System;
using Systems.MineSystem.MinePlayerSystem.Service;
using Systems.MineSystem.MinePlayerSystem.Scriptable;
using UniRx;
using UnityEngine;

namespace Systems.MineSystem.HealthStaminaSystem.Model
{
    [Serializable]
    public class HealthStaminaModel : IDisposable
    {
        private CompositeDisposable _disposables;

        private MinePlayerScriptable _minePlayerScriptable;
        private readonly IPlayerDamageService _damageService;
        public IReadOnlyReactiveProperty<float> Health => _minePlayerScriptable?.playerData.health;
        public IReadOnlyReactiveProperty<float> MaxHealth => _minePlayerScriptable?.playerData.maxHealth;
        public IReadOnlyReactiveProperty<float> Stamina => _minePlayerScriptable?.playerData.stamina;
        public IReadOnlyReactiveProperty<float> MaxStamina => _minePlayerScriptable?.playerData.maxStamina;

        public HealthStaminaModel(
            MinePlayerScriptable minePlayerScriptable,
            IPlayerDamageService damageService)
        {
            _disposables = new CompositeDisposable();
            _minePlayerScriptable = minePlayerScriptable;
            _damageService = damageService;

            minePlayerScriptable.playerData.maxHealth.Value = 100;
            minePlayerScriptable.playerData.maxStamina.Value = 100;
            IncreaseHealth(100);
            IncreaseStamina(100);
        }

        #region Health

        public void IncreaseHealth(float value)
        {
            var health = _minePlayerScriptable.playerData.health.Value;
            var maxHealth = _minePlayerScriptable.playerData.maxHealth.Value;

            var modifiedHealth = health + value;
            modifiedHealth = Mathf.Clamp(0, modifiedHealth, maxHealth);
            _minePlayerScriptable.playerData.health.Value = modifiedHealth;
        }

        public void ReduceHealth(float value)
        {
            _damageService.ApplyDamage(value);
        }

        #endregion

        #region Stamina

        public void IncreaseStamina(float value)
        {
            var stamina = _minePlayerScriptable.playerData.stamina.Value;
            var maxStamina = _minePlayerScriptable.playerData.maxStamina.Value;
            
            var modifiedStamina = stamina + value;
            modifiedStamina = Mathf.Clamp(0, modifiedStamina, maxStamina);
            _minePlayerScriptable.playerData.stamina.Value = modifiedStamina;
        }

        public void ReduceStamina(float value)
        {
            var stamina = _minePlayerScriptable.playerData.stamina.Value;
            var maxStamina = _minePlayerScriptable.playerData.maxStamina.Value;
            
            var modifiedStamina = stamina - value;
            modifiedStamina = Mathf.Clamp(0, modifiedStamina, maxStamina);
            _minePlayerScriptable.playerData.stamina.Value = modifiedStamina; 
        }

        #endregion

        public void Dispose()
        {
            _disposables?.Dispose();
        }
    }
}
