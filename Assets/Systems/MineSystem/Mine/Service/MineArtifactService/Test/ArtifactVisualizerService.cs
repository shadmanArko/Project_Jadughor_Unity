using System;
using System.Collections.Generic;
using Systems.MineSystem.Mine.Model;
using Systems.MineSystem.Mine.View;
using Systems.MineSystem.MinePlayerSystem.Scriptable;
using UniRx;
using UnityEngine;
using UnityEngine.Tilemaps;
using Zenject;

namespace Systems.MineSystem.Mine.Service.MineArtifactService.Test
{
    [Serializable]
    public sealed class ArtifactVisualizerService : IInitializable, IDisposable
    {
        private readonly MineView _view;
        private readonly MinePlayerScriptable _player;
        private readonly ArtifactSpriteScriptable _sprites;
        private readonly ArtifactCollectionService _collectionService;
        private readonly Dictionary<string, Tile> _tilesByDefinitionId = new();
        private readonly CompositeDisposable _disposables = new();

        private MineData _mineData;

        public ArtifactVisualizerService(
            MineView view,
            MinePlayerScriptable player,
            ArtifactSpriteScriptable sprites,
            ArtifactCollectionService collectionService)
        {
            _view = view;
            _player = player;
            _sprites = sprites;
            _collectionService = collectionService;
        }

        public void Initialize()
        {
            _collectionService.ArtifactRemovedFromCell
                .Subscribe(ClearArtifactTile)
                .AddTo(_disposables);
        }

        public void SetMineData(MineData mineData)
        {
            _mineData = mineData;
        }

        public void UpdateArtifactTile(Cell cell)
        {
            if (_view.artifactTileMap == null)
                return;

            if (!cell.HasArtifact)
            {
                _view.artifactTileMap.SetTile(cell.GetPosition(), null);
                return;
            }

            var artifact = _mineData?.GetArtifact(cell.Id);
            if (artifact == null)
            {
                Debug.LogWarning(
                    $"Artifact instance '{cell.ItemId}' was not found for cell '{cell.Id}'.");
                _view.artifactTileMap.SetTile(cell.GetPosition(), null);
                return;
            }

            if (!TryGetTile(artifact.DefinitionId, out var tile))
            {
                Debug.LogWarning(
                    $"ArtifactSpriteScriptable has no world sprite for definition " +
                    $"'{artifact.DefinitionId}' in {_player.region}/{_player.site}.");
                _view.artifactTileMap.SetTile(cell.GetPosition(), null);
                return;
            }

            _view.artifactTileMap.SetTile(cell.GetPosition(), tile);
        }

        private bool TryGetTile(string definitionId, out Tile tile)
        {
            if (_tilesByDefinitionId.TryGetValue(definitionId, out tile))
                return true;

            var sprite = _sprites.GetWorldSprite(
                definitionId,
                _player.region,
                _player.site);

            if (sprite == null)
                return false;

            tile = ScriptableObject.CreateInstance<Tile>();
            tile.name = $"ArtifactTile_{definitionId}";
            tile.sprite = sprite;
            _tilesByDefinitionId.Add(definitionId, tile);
            return true;
        }

        private void ClearArtifactTile(string cellId)
        {
            if (_mineData?.Cells == null || string.IsNullOrEmpty(cellId))
                return;

            foreach (var cell in _mineData.Cells)
            {
                if (cell.Id != cellId)
                    continue;

                _view.artifactTileMap?.SetTile(cell.GetPosition(), null);
                return;
            }
        }

        public void Dispose()
        {
            _disposables.Dispose();

            foreach (var tile in _tilesByDefinitionId.Values)
            {
                if (tile != null)
                    UnityEngine.Object.Destroy(tile);
            }

            _tilesByDefinitionId.Clear();
            _mineData = null;
        }
    }
}
