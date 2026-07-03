using System;
using Systems.MineSystem.Mine.Model;
using Systems.MineSystem.Mine.Scriptable;
using Systems.MineSystem.Mine.View;
using Systems.MineSystem.MinePlayerSystem.Scriptable;
using UniRx;
using UnityEngine;
using Zenject;

namespace Systems.MineSystem.Mine.Service.Lighting
{
    public sealed class MineDarkeningService : IInitializable, IDisposable
    {
        private readonly MineModel _mine;
        private readonly MineView _view;
        private readonly RuntimeDataScriptable _playerRuntime;
        private readonly MineDarkeningConfig _config;
        private readonly CompositeDisposable _disposables = new();

        public MineDarkeningService(
            MineModel mine,
            MineView view,
            RuntimeDataScriptable playerRuntime,
            MineDarkeningConfig config)
        {
            _mine = mine;
            _view = view;
            _playerRuntime = playerRuntime;
            _config = config;
        }

        public void Initialize()
        {
            if (_view.darkeningShaderRenderer == null)
                throw new InvalidOperationException(
                    "MineView requires a DarkeningShader SpriteRenderer reference.");

            SetAlpha(0f);
            _mine.MineData
                .Where(data => data != null)
                .Subscribe(SizeToMine)
                .AddTo(_disposables);
            _playerRuntime.worldPosition
                .Subscribe(UpdateAlpha)
                .AddTo(_disposables);
        }

        private void SizeToMine(MineData mineData)
        {
            if (mineData.GridWidth <= 0 || mineData.GridHeight <= 0)
                return;

            var minCell = new Vector3Int(
                -(mineData.GridWidth / 2),
                -(mineData.GridHeight - 1), 0);
            var maxCell = new Vector3Int(
                minCell.x + mineData.GridWidth - 1, 0, 0);
            var minCenter = _view.grid.GetCellCenterWorld(minCell);
            var maxCenter = _view.grid.GetCellCenterWorld(maxCell);
            var cellOrigin = _view.grid.CellToWorld(Vector3Int.zero);
            var cellStep = _view.grid.CellToWorld(Vector3Int.one) - cellOrigin;
            var mineWidth = Mathf.Abs(cellStep.x) * mineData.GridWidth;
            var mineHeight = Mathf.Abs(cellStep.y) * mineData.GridHeight;

            var renderer = _view.darkeningShaderRenderer;
            var position = (minCenter + maxCenter) * 0.5f;
            position.z = renderer.transform.position.z;
            renderer.transform.position = position;

            var spriteSize = renderer.sprite != null
                ? renderer.sprite.bounds.size
                : Vector3.one;
            var parentScale = renderer.transform.parent != null
                ? renderer.transform.parent.lossyScale
                : Vector3.one;
            renderer.transform.localScale = new Vector3(
                mineWidth / (spriteSize.x * Mathf.Abs(parentScale.x)),
                mineHeight / (spriteSize.y * Mathf.Abs(parentScale.y)),
                1f);
        }

        private void UpdateAlpha(Vector2 playerWorldPosition)
        {
            var playerCellY = _view.grid.WorldToCell(playerWorldPosition).y;
            var fade = Mathf.InverseLerp(
                _config.fadeStartCellY,
                _config.maxAlphaCellY,
                playerCellY);
            SetAlpha(fade * (_config.maxAlpha / 255f));
        }

        private void SetAlpha(float alpha)
        {
            var renderer = _view.darkeningShaderRenderer;
            var color = renderer.color;
            color.a = Mathf.Clamp(alpha, 0f, _config.maxAlpha / 255f);
            renderer.color = color;
        }

        public void Dispose()
        {
            _disposables.Dispose();
        }
    }
}
