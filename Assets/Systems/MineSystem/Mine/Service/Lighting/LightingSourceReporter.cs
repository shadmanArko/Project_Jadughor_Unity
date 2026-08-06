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

        [SerializeField, Tooltip("Culling radius for light shapes the reporter " +
             "cannot measure (Freeform / Parametric, which expose no public " +
             "bounds). Ignored when zero or less.")]
        private float radiusOverride = -1f;

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
                radius = Mathf.Max(radius, GetLocalRadius(light) * largestScale);
            }

            return radius;
        }

        private float GetLocalRadius(Light2D light)
        {
            if (radiusOverride > 0f)
                return radiusOverride;

            switch (light.lightType)
            {
                case Light2D.LightType.Point:
                    return light.pointLightOuterRadius;
                case Light2D.LightType.Sprite:
                    // Sprite lights leave pointLightOuterRadius at its authored
                    // value, which has nothing to do with the cookie's size, so
                    // measure the sprite instead. Without this the culling margin
                    // collapses and the light pops off at the screen edge.
                    var cookie = light.lightCookieSprite;
                    if (cookie == null)
                        return 0f;
                    var extents = cookie.bounds.extents;
                    return Mathf.Max(extents.x, extents.y);
                default:
                    // Freeform / Parametric expose no public bounds - set
                    // radiusOverride on the reporter for those.
                    return 0f;
            }
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
