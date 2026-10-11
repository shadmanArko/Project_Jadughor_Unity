using System;
using Systems.MineSystem.EnemySystem.Mob.HedgehogBoss.Config;
using Systems.MineSystem.EnemySystem.Mob.HedgehogBoss.Enum;
using UniRx;
using UnityEngine;

namespace Systems.MineSystem.EnemySystem.Mob.HedgehogBoss.Model
{
    /// <summary>
    /// Mutable combat state for one Hedgehog lair encounter. Unity objects and
    /// presentation stay in the view/state machine; this model owns only the
    /// values required to make decisions consistently across fixed ticks.
    /// </summary>
    public sealed class HedgehogBossModel : IDisposable
    {
        private readonly ReactiveProperty<int> _health = new();
        private readonly ReactiveProperty<int> _maximumHealth = new();
        private bool _disposed;

        public HedgehogBossState State { get; private set; } =
            HedgehogBossState.Dormant;
        public IReadOnlyReactiveProperty<int> Health => _health;
        public IReadOnlyReactiveProperty<int> MaximumHealth => _maximumHealth;
        public float AttackCooldownRemaining { get; private set; }
        public float RecoveryRemaining { get; private set; }
        public float ChargeRemaining { get; private set; }
        public bool AttackImpactApplied { get; set; }
        public bool ChargeContactApplied { get; set; }
        public bool RewardsDropped { get; set; }
        public bool FacesLeft { get; set; }
        public HedgehogBossPhaseData ActivePhase { get; private set; }

        public bool IsDead => _health.Value <= 0;

        public void Initialize(HedgehogBossConfigScriptable config)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            _maximumHealth.Value = config.MaxHealth;
            _health.Value = _maximumHealth.Value;
            AttackCooldownRemaining = 0f;
            RecoveryRemaining = 0f;
            ChargeRemaining = 0f;
            AttackImpactApplied = false;
            ChargeContactApplied = false;
            RewardsDropped = false;
            FacesLeft = false;
            State = HedgehogBossState.Dormant;
            ActivePhase = config.ResolvePhase(100f);
        }

        public bool ApplyDamage(float amount, HedgehogBossConfigScriptable config)
        {
            if (_disposed || IsDead || amount <= 0f || config == null)
                return false;

            _health.Value = Mathf.Max(
                0, _health.Value - Mathf.CeilToInt(amount));
            var remainingPercent = _maximumHealth.Value <= 0
                ? 0f
                : _health.Value * 100f / _maximumHealth.Value;
            ActivePhase = config.ResolvePhase(remainingPercent);
            return true;
        }

        public void SetState(HedgehogBossState state)
        {
            if (_disposed)
                return;
            State = state;
            AttackImpactApplied = false;
            if (state != HedgehogBossState.RollCharge)
                ChargeContactApplied = false;
        }

        public void StartAttackCooldown(float cooldown) =>
            AttackCooldownRemaining = Mathf.Max(0f, cooldown);

        public void StartRecovery(float recovery) =>
            RecoveryRemaining = Mathf.Max(0f, recovery);

        public void StartCharge(float duration) =>
            ChargeRemaining = Mathf.Max(0f, duration);

        public void Tick(float deltaTime)
        {
            if (_disposed || deltaTime <= 0f)
                return;
            AttackCooldownRemaining = Mathf.Max(0f, AttackCooldownRemaining - deltaTime);
            RecoveryRemaining = Mathf.Max(0f, RecoveryRemaining - deltaTime);
            ChargeRemaining = Mathf.Max(0f, ChargeRemaining - deltaTime);
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _health.Value = 0;
            _maximumHealth.Value = 0;
            _health.Dispose();
            _maximumHealth.Dispose();
            ActivePhase = null;
            State = HedgehogBossState.Defeated;
        }
    }
}
