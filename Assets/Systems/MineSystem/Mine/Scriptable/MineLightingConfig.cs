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
        [SerializeField] public MaterialReactiveProperty maskMaterial =
            new();
        [SerializeField] public ColorReactiveProperty tintColor =
            new(new Color(0.06f, 0.02f, 0.13f, 0.78f));
        [SerializeField] public FloatReactiveProperty softness = new(0.35f);
        [SerializeField] public FloatReactiveProperty mineBoundsPadding =
            new(0f);
        [SerializeField] public FloatReactiveProperty alphaFadeDepthCells =
            new(5f);
        [SerializeField] public FloatReactiveProperty topAlpha = new(0f);
        [SerializeField] public FloatReactiveProperty bottomAlphaMultiplier =
            new(1f);
        [SerializeField] public FloatReactiveProperty playerRadius = new(4f);
        [SerializeField] public FloatReactiveProperty torchRadius = new(4.5f);
        [SerializeField] public FloatReactiveProperty fallbackLightRadius =
            new(3.5f);
        [SerializeField] public FloatReactiveProperty lightIntensityScale =
            new(1f);
        [SerializeField] public IntReactiveProperty maxLights = new(32);
        [SerializeField] public StringReactiveProperty overlaySortingLayer =
            new("PlaceablePreview");
        [SerializeField] public IntReactiveProperty overlaySortingOrder =
            new(500);

        private readonly Subject<Unit> _validated = new();

        public Material MaskMaterial => maskMaterial.Value;
        public Color TintColor => tintColor.Value;
        public float Softness => Mathf.Clamp(softness.Value, 0.01f, 1f);
        public float MineBoundsPadding =>
            Mathf.Max(0f, mineBoundsPadding.Value);
        public float AlphaFadeDepthCells =>
            Mathf.Max(0.01f, alphaFadeDepthCells.Value);
        public float TopAlpha => Mathf.Clamp01(topAlpha.Value);
        public float BottomAlphaMultiplier =>
            Mathf.Max(0f, bottomAlphaMultiplier.Value);
        public float PlayerRadius => Mathf.Max(0.01f, playerRadius.Value);
        public float TorchRadius => Mathf.Max(0.01f, torchRadius.Value);
        public float FallbackLightRadius =>
            Mathf.Max(0.01f, fallbackLightRadius.Value);
        public float LightIntensityScale =>
            Mathf.Max(0f, lightIntensityScale.Value);
        public int MaxLights => Mathf.Clamp(maxLights.Value, 1, 32);
        public string OverlaySortingLayer => overlaySortingLayer.Value;
        public int OverlaySortingOrder => overlaySortingOrder.Value;

        public IObservable<Unit> ObserveChanged()
        {
            return Observable.Merge(
                maskMaterial.SkipLatestValueOnSubscribe().AsUnitObservable(),
                tintColor.SkipLatestValueOnSubscribe().AsUnitObservable(),
                softness.SkipLatestValueOnSubscribe().AsUnitObservable(),
                mineBoundsPadding.SkipLatestValueOnSubscribe()
                    .AsUnitObservable(),
                alphaFadeDepthCells.SkipLatestValueOnSubscribe()
                    .AsUnitObservable(),
                topAlpha.SkipLatestValueOnSubscribe().AsUnitObservable(),
                bottomAlphaMultiplier.SkipLatestValueOnSubscribe()
                    .AsUnitObservable(),
                playerRadius.SkipLatestValueOnSubscribe().AsUnitObservable(),
                torchRadius.SkipLatestValueOnSubscribe().AsUnitObservable(),
                fallbackLightRadius.SkipLatestValueOnSubscribe()
                    .AsUnitObservable(),
                lightIntensityScale.SkipLatestValueOnSubscribe()
                    .AsUnitObservable(),
                maxLights.SkipLatestValueOnSubscribe().AsUnitObservable(),
                overlaySortingLayer.SkipLatestValueOnSubscribe()
                    .AsUnitObservable(),
                overlaySortingOrder.SkipLatestValueOnSubscribe()
                    .AsUnitObservable(),
                _validated);
        }

        private void OnValidate()
        {
            softness.Value = Mathf.Clamp(softness.Value, 0.01f, 1f);
            mineBoundsPadding.Value = Mathf.Max(0f, mineBoundsPadding.Value);
            alphaFadeDepthCells.Value =
                Mathf.Max(0.01f, alphaFadeDepthCells.Value);
            topAlpha.Value = Mathf.Clamp01(topAlpha.Value);
            bottomAlphaMultiplier.Value =
                Mathf.Max(0f, bottomAlphaMultiplier.Value);
            playerRadius.Value = Mathf.Max(0.01f, playerRadius.Value);
            torchRadius.Value = Mathf.Max(0.01f, torchRadius.Value);
            fallbackLightRadius.Value =
                Mathf.Max(0.01f, fallbackLightRadius.Value);
            lightIntensityScale.Value =
                Mathf.Max(0f, lightIntensityScale.Value);
            maxLights.Value = Mathf.Clamp(maxLights.Value, 1, 32);
            _validated.OnNext(Unit.Default);
        }

        [Serializable]
        public sealed class MaterialReactiveProperty :
            ReactiveProperty<Material>
        {
            public MaterialReactiveProperty()
            {
            }

            public MaterialReactiveProperty(Material initialValue)
                : base(initialValue)
            {
            }
        }

        [Serializable]
        public sealed class ColorReactiveProperty :
            ReactiveProperty<Color>
        {
            public ColorReactiveProperty()
            {
            }

            public ColorReactiveProperty(Color initialValue)
                : base(initialValue)
            {
            }
        }

        [Serializable]
        public sealed class FloatReactiveProperty :
            ReactiveProperty<float>
        {
            public FloatReactiveProperty()
            {
            }

            public FloatReactiveProperty(float initialValue)
                : base(initialValue)
            {
            }
        }

        [Serializable]
        public sealed class IntReactiveProperty :
            ReactiveProperty<int>
        {
            public IntReactiveProperty()
            {
            }

            public IntReactiveProperty(int initialValue)
                : base(initialValue)
            {
            }
        }

        [Serializable]
        public sealed class StringReactiveProperty :
            ReactiveProperty<string>
        {
            public StringReactiveProperty()
            {
            }

            public StringReactiveProperty(string initialValue)
                : base(initialValue)
            {
            }
        }
    }
}
