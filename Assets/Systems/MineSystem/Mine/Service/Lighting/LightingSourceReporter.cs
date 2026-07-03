using System.Collections.Generic;
using Systems.MineSystem.Mine.Signal;
using Systems.Utilities.EventBus;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Systems.MineSystem.Mine.Service.Lighting
{
    [DisallowMultipleComponent]
    public sealed class LightingSourceReporter : MonoBehaviour
    {
        [SerializeField] private bool managedByLightingManager = true;

        private Light2D[] _lights;
        private bool[] _authoredEnabled;
        private bool _started;
        private bool _registered;

        public bool ManagedByLightingManager => managedByLightingManager;
        public IReadOnlyList<Light2D> Lights => _lights;
        public Transform CachedTransform => transform;
        public bool IsSourceActive => isActiveAndEnabled;

        private void Awake()
        {
            _lights = GetComponentsInChildren<Light2D>(true);
            _authoredEnabled = new bool[_lights.Length];
            for (var i = 0; i < _lights.Length; i++)
            {
                _authoredEnabled[i] = _lights[i].enabled;
            }
        }

        private void Start()
        {
            _started = true;
            RegisterIfManaged();
        }

        private void OnEnable()
        {
            if (!_started)
                return;

            RegisterIfManaged();
            if (_registered)
                GlobalEventBus.Fire(new LightingSourceActivationChangedSignal(
                    this, true));
        }

        private void OnDisable()
        {
            if (_registered)
                GlobalEventBus.Fire(new LightingSourceActivationChangedSignal(
                    this, false));
        }

        private void OnDestroy()
        {
            Unregister();
        }

        private void OnValidate()
        {
            if (!Application.isPlaying || !_started)
                return;

            SynchronizeManagement();
        }

        public void SetManagedByLightingManager(bool managed)
        {
            if (managedByLightingManager == managed)
                return;

            managedByLightingManager = managed;
            if (!_started)
                return;
            SynchronizeManagement();
        }

        public bool WasAuthoredEnabled(int index) =>
            index >= 0 && index < _authoredEnabled.Length &&
            _authoredEnabled[index];

        public float GetEffectiveRadius()
        {
            var radius = 0f;
            for (var i = 0; i < _lights.Length; i++)
            {
                var light = _lights[i];
                if (light == null || light.lightType == Light2D.LightType.Global)
                    continue;

                var scale = light.transform.lossyScale;
                var largestScale = Mathf.Max(Mathf.Abs(scale.x),
                    Mathf.Abs(scale.y));
                radius = Mathf.Max(radius,
                    light.pointLightOuterRadius * largestScale);
            }

            return radius;
        }

        public void RestoreAuthoredState()
        {
            for (var i = 0; i < _lights.Length; i++)
            {
                if (_lights[i] == null ||
                    _lights[i].lightType == Light2D.LightType.Global)
                    continue;
                _lights[i].enabled = _authoredEnabled[i];
            }
        }

        private void RegisterIfManaged()
        {
            if (!managedByLightingManager || _registered ||
                _lights == null || _lights.Length == 0)
                return;

            _registered = true;
            GlobalEventBus.Fire(new LightingSourceRegisteredSignal(this));
        }

        private void SynchronizeManagement()
        {
            if (managedByLightingManager)
            {
                RegisterIfManaged();
                return;
            }

            RestoreAuthoredState();
            Unregister();
        }

        private void Unregister()
        {
            if (!_registered)
                return;

            _registered = false;
            GlobalEventBus.Fire(new LightingSourceUnregisteredSignal(this));
        }
    }
}
