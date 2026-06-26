using System;
using System.Collections.Generic;
using System.Reflection;
using Systems.MineSystem.Mine.Model;
using Systems.MineSystem.Mine.Scriptable;
using Systems.MineSystem.MinePlayerSystem.View;
using UniRx;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Zenject;

namespace Systems.MineSystem.Mine.Service.Lighting
{
    public sealed class MineLightingService : IInitializable, IDisposable
    {
        private const string OverlayName = "Mine Interior Light Mask";
        private const float MaxTintAlpha = 150f / 255f;
        private static readonly int TintColorId =
            Shader.PropertyToID("_TintColor");
        private static readonly int SoftnessId =
            Shader.PropertyToID("_Softness");
        private static readonly int LightCountId =
            Shader.PropertyToID("_LightCount");
        private static readonly int LightDataId =
            Shader.PropertyToID("_LightData");

        private readonly Camera _camera;
        private readonly MineLightingConfig _config;
        private readonly MineModel _mineModel;
        private readonly PlayerView _playerView;
        private readonly CompositeDisposable _disposables = new();
        private readonly Vector4[] _lightData = new Vector4[32];
        private GameObject _overlayObject;
        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        private Material _runtimeMaterial;
        private Mesh _mesh;
        private bool _insideMine = true;

        public MineLightingService(
            Camera camera,
            MineLightingConfig config,
            MineModel mineModel,
            [InjectOptional] PlayerView playerView = null)
        {
            _camera = camera;
            _config = config;
            _mineModel = mineModel;
            _playerView = playerView;
        }

        public void Initialize()
        {
            EnsureOverlay();
            ApplyConfig();

            _config.ObserveChanged()
                .Subscribe(_ => ApplyConfig())
                .AddTo(_disposables);

            Observable.EveryLateUpdate()
                .Subscribe(_ => RefreshOverlay())
                .AddTo(_disposables);
        }

        public void SetInsideMineLighting(bool inside)
        {
            _insideMine = inside;
            if (_overlayObject != null)
                _overlayObject.SetActive(inside);
        }

        public void Dispose()
        {
            _disposables.Dispose();
            if (_runtimeMaterial != null)
                UnityEngine.Object.Destroy(_runtimeMaterial);
            if (_mesh != null)
                UnityEngine.Object.Destroy(_mesh);
            if (_overlayObject != null)
                UnityEngine.Object.Destroy(_overlayObject);
        }

        private void EnsureOverlay()
        {
            if (_overlayObject != null)
                return;

            _overlayObject = new GameObject(OverlayName);
            _overlayObject.transform.SetPositionAndRotation(
                Vector3.zero,
                Quaternion.identity);

            _meshFilter = _overlayObject.AddComponent<MeshFilter>();
            _meshRenderer = _overlayObject.AddComponent<MeshRenderer>();

            _mesh = new Mesh { name = "Mine Interior Light Mask Mesh" };
            _mesh.MarkDynamic();
            _meshFilter.sharedMesh = _mesh;
        }

        private void ApplyConfig()
        {
            EnsureOverlay();
            ApplyMaterial();
            ApplyRendererSorting();
            RefreshOverlay();
        }

        private void ApplyMaterial()
        {
            var sourceMaterial = _config.MaskMaterial;
            if (sourceMaterial == null)
                return;

            if (_runtimeMaterial == null ||
                _runtimeMaterial.shader != sourceMaterial.shader)
            {
                if (_runtimeMaterial != null)
                    UnityEngine.Object.Destroy(_runtimeMaterial);
                _runtimeMaterial = new Material(sourceMaterial)
                {
                    name = "Runtime Mine Interior Light Mask"
                };
                _meshRenderer.sharedMaterial = _runtimeMaterial;
            }

            _runtimeMaterial.SetFloat(SoftnessId, _config.Softness);
        }

        private void ApplyRendererSorting()
        {
            if (_meshRenderer == null)
                return;

            if (!string.IsNullOrWhiteSpace(_config.OverlaySortingLayer))
                _meshRenderer.sortingLayerName =
                    _config.OverlaySortingLayer;
            _meshRenderer.sortingOrder = _config.OverlaySortingOrder;
        }

        private void RefreshOverlay()
        {
            if (!_insideMine ||
                _camera == null ||
                _mesh == null)
                return;

            ApplyMaterial();
            ApplyRendererSorting();
            if (_runtimeMaterial == null)
                return;

            RefreshTintAlpha();
            RefreshMesh();
            RefreshLightData();
        }

        private void RefreshMesh()
        {
            if (!TryGetMineBounds(out var minX, out var maxX,
                    out var minY, out var maxY))
                return;

            _mesh.Clear();
            _mesh.vertices = new[]
            {
                new Vector3(minX, minY, 0f),
                new Vector3(minX, maxY, 0f),
                new Vector3(maxX, maxY, 0f),
                new Vector3(maxX, minY, 0f)
            };
            _mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            _mesh.RecalculateBounds();
        }

        private void RefreshTintAlpha()
        {
            var color = _config.TintColor;
            color.a = Mathf.Min(
                MaxTintAlpha * GetDepthAlphaMultiplier(),
                MaxTintAlpha);
            _runtimeMaterial.SetColor(TintColorId, color);
        }

        private float GetDepthAlphaMultiplier()
        {
            if (!TryGetMineTopY(out var mineTopY))
                return _config.BottomAlphaMultiplier;

            var playerY = GetPlayerY();
            var playerDepth = mineTopY - playerY;
            var depthFactor = Mathf.InverseLerp(
                0f,
                _config.AlphaFadeDepthCells,
                playerDepth);
            return Mathf.Lerp(
                _config.TopAlpha,
                _config.BottomAlphaMultiplier,
                depthFactor);
        }

        private float GetPlayerY()
        {
            if (_playerView != null)
                return _playerView.transform.position.y;

            var playerObject = GameObject.Find("Player");
            return playerObject != null
                ? playerObject.transform.position.y
                : _camera.transform.position.y;
        }

        private bool TryGetMineTopY(out float mineTopY)
        {
            mineTopY = 0f;
            var mineData = _mineModel.MineData.Value;
            if (mineData == null ||
                mineData.GridWidth <= 0 ||
                mineData.GridHeight <= 0)
                return false;

            mineTopY = 0f;
            return true;
        }

        private bool TryGetMineBounds(
            out float minX,
            out float maxX,
            out float minY,
            out float maxY)
        {
            minX = maxX = minY = maxY = 0f;
            var mineData = _mineModel.MineData.Value;
            if (mineData == null ||
                mineData.GridWidth <= 0 ||
                mineData.GridHeight <= 0)
                return false;

            var padding = _config.MineBoundsPadding;
            minX = -(mineData.GridWidth * 0.5f) - padding;
            maxX = minX + mineData.GridWidth + padding * 2f;
            minY = -mineData.GridHeight - padding;
            maxY = 0f + padding;
            return true;
        }

        private void RefreshLightData()
        {
            var lights = UnityEngine.Object.FindObjectsByType<Light2D>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);

            var count = 0;
            for (var i = 0; i < lights.Length; i++)
            {
                var light = lights[i];
                if (light == null ||
                    !light.enabled ||
                    IsGlobalLight(light) ||
                    count >= _config.MaxLights)
                    continue;

                var radius = GetConfiguredRadius(light);
                var intensity = Mathf.Clamp01(
                    GetLightIntensity(light) * _config.LightIntensityScale);
                var position = light.transform.position;
                _lightData[count] = new Vector4(
                    position.x,
                    position.y,
                    radius,
                    intensity);
                count++;
            }

            _runtimeMaterial.SetInt(LightCountId, count);
            _runtimeMaterial.SetVectorArray(LightDataId, _lightData);
        }

        private float GetConfiguredRadius(Component light)
        {
            if (HasNameInHierarchy(light.transform, "Player"))
                return _config.PlayerRadius;
            if (HasNameInHierarchy(light.transform, "Torch"))
                return _config.TorchRadius;

            var lightRadius = GetFloatProperty(
                light,
                "pointLightOuterRadius",
                _config.FallbackLightRadius);
            return Mathf.Max(lightRadius, _config.FallbackLightRadius);
        }

        private static float GetLightIntensity(Component light)
        {
            return GetFloatProperty(light, "intensity", 1f);
        }

        private static float GetFloatProperty(
            Component component,
            string propertyName,
            float fallback)
        {
            var property = component.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public);
            if (property == null)
                return fallback;

            var value = property.GetValue(component);
            return value is float floatValue ? floatValue : fallback;
        }

        private static bool IsGlobalLight(Component light)
        {
            var property = light.GetType().GetProperty("lightType");
            var value = property?.GetValue(light);
            return value != null &&
                   string.Equals(
                       value.ToString(),
                       "Global",
                       StringComparison.Ordinal);
        }

        private static bool HasNameInHierarchy(Transform transform, string text)
        {
            while (transform != null)
            {
                if (transform.name.IndexOf(
                        text,
                        StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;

                transform = transform.parent;
            }

            return false;
        }
    }
}
