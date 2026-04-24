using System;
using Systems.MineSystem.MinePlayerSystem.Model;
using Systems.MineSystem.MinePlayerSystem.Scriptable;
using UniRx;
using UnityEngine;

namespace Systems.MineSystem.HealthStaminaSystem.Model
{
    [Serializable]
    public class HealthStaminaModel : IDisposable
    {
        private CompositeDisposable _disposables;

        private MinePlayerData _minePlayerData;
        public IReadOnlyReactiveProperty<float> Health => _minePlayerData?.health;
        public IReadOnlyReactiveProperty<float> MaxHealth => _minePlayerData?.maxHealth;
        public IReadOnlyReactiveProperty<float> Stamina => _minePlayerData?.stamina;
        public IReadOnlyReactiveProperty<float> MaxStamina => _minePlayerData?.maxStamina;

        public HealthStaminaModel(MinePlayerScriptable minePlayerScriptable)
        {
            _disposables = new CompositeDisposable();
            _minePlayerData = minePlayerScriptable.playerData;
        }

        #region Health

        public void IncreaseHealth(float value)
        {
            var health = _minePlayerData.health.Value;
            var maxHealth = _minePlayerData.maxHealth.Value;

            var modifiedHealth = health + value;
            modifiedHealth = Mathf.Clamp(0, modifiedHealth, maxHealth);
            _minePlayerData.health.Value = modifiedHealth;
        }

        public void ReduceHealth(float value)
        {
            var health = _minePlayerData.health.Value;
            var maxHealth = _minePlayerData.maxHealth.Value;
            
            var modifiedHealth = health - value;
            modifiedHealth = Mathf.Clamp(0, modifiedHealth, maxHealth);
            _minePlayerData.health.Value = modifiedHealth;
        }

        #endregion

        #region Stamina

        public void IncreaseStamina(float value)
        {
            var stamina = _minePlayerData.stamina.Value;
            var maxStamina = _minePlayerData.maxStamina.Value;
            
            var modifiedStamina = stamina + value;
            modifiedStamina = Mathf.Clamp(0, modifiedStamina, maxStamina);
            _minePlayerData.stamina.Value = modifiedStamina;
        }

        public void ReduceStamina(float value)
        {
            var stamina = _minePlayerData.stamina.Value;
            var maxStamina = _minePlayerData.maxStamina.Value;
            
            var modifiedStamina = stamina - value;
            modifiedStamina = Mathf.Clamp(0, modifiedStamina, maxStamina);
            _minePlayerData.stamina.Value = modifiedStamina; 
        }

        #endregion

        public void Dispose()
        {
            _disposables?.Dispose();
        }
    }
}