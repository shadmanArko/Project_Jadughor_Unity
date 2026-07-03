using System;
using UniRx;
using UnityEngine;

namespace Systems.MineSystem.Mine.Scriptable
{
    [CreateAssetMenu(
        fileName = "MineLightingConfig",
        menuName = "Mine/Lighting Config")]
    public sealed class MineLightingConfig : ScriptableObject
    {
        [Header("Light source culling")]
        [SerializeField, Min(0f)] private float safeAreaMargin = 2f;
        [SerializeField, Min(0f)] private float exitHysteresis = 0.5f;
        [SerializeField, Min(0.01f)] private float movementThreshold = 0.1f;

        private readonly Subject<Unit> _validated = new();

        public float SafeAreaMargin => Mathf.Max(0f, safeAreaMargin);
        public float ExitHysteresis => Mathf.Max(0f, exitHysteresis);
        public float MovementThreshold => Mathf.Max(0.01f, movementThreshold);

        public IObservable<Unit> ObserveChanged() => _validated;

        private void OnValidate()
        {
            safeAreaMargin = Mathf.Max(0f, safeAreaMargin);
            exitHysteresis = Mathf.Max(0f, exitHysteresis);
            movementThreshold = Mathf.Max(0.01f, movementThreshold);
            _validated.OnNext(Unit.Default);
        }
    }
}
