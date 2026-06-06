using System;
using Systems.MineSystem.Mine.Controller;
using Systems.MineSystem.Mine.Model;
using Systems.MineSystem.Mine.Service.MineArtifactService.Test;
using Systems.MineSystem.Mine.View;
using Systems.MineSystem.MinePlayerSystem.Scriptable;
using Systems.Utilities.Injector;
using UniRx;
using UnityEngine;
using UnityEngine.Tilemaps;
using Zenject;

namespace Systems.MineSystem
{
    public class WallBreakerTest : MonoBehaviour
    {
        [Inject] private MineController _mineController;
        [Inject] private MineModel _mineModel;
        [Inject] private Camera _cam;
        [Inject] private MineView _view;
        [Inject] private ArtifactSpriteScriptable _artifactSprites;
        [Inject] private MinePlayerScriptable _player;
        private Tilemap _targetTilemap;
        private readonly CompositeDisposable _disposables = new();

        private InputSystem_Actions _inputMaster;

        private void Start()
        {
            ManualInjector.InjectDependencies(this);

            _mineModel.MineData
                .Where(mineData => mineData != null)
                .Subscribe(mineData =>
                    Debug.Log(
                        $"Mine generated with {mineData.Artifacts?.Count ?? 0} artifacts " +
                        $"and {mineData.ArtifactPlacements?.Count ?? 0} placements."))
                .AddTo(_disposables);

            _mineModel.OnArtifactDiscovered
                .Subscribe(artifact =>
                    Debug.Log(
                        $"Artifact discovered: {artifact.Name} " +
                        $"[{artifact.DefinitionId}], {artifact.Rarity}, {artifact.Condition}."))
                .AddTo(_disposables);
        }
        
        private void Update()
        {
            if (Input.GetMouseButtonDown(0)) // Left click
            {
                if (TryGetTileAtMouse_NoPhysics(_view.wallTileMap, out var cellPos))
                {
                    Debug.Log($"Clicked tile at {cellPos}");
                    _mineController.HitWall(cellPos);
                }
                else
                {
                    Debug.Log("No tile found");
                }
            }
            
            if (Input.GetMouseButtonDown(1)) // Left click
            {
                if (TryGetTileAtMouse_NoPhysics(_view.unrevealedTileMap, out var cellPos))
                {
                    Debug.Log($"Clicked tile at {cellPos}");
                    var cell = _mineModel.MineData.Value.GetCell(cellPos);
                    if (cell == null)
                        return;

                    var artifact = _mineModel.MineData.Value.GetArtifact(cell.Id);
                    var hasArtifactSprite = artifact != null &&
                                            _artifactSprites.GetWorldSprite(
                                                artifact.DefinitionId,
                                                _player.region,
                                                _player.site) != null;

                    Debug.Log(
                        $"Cell Position: {cell.Position}, " +
                        $"hasResource: {cell.HasResource}, " +
                        $"hasArtifact: {cell.HasArtifact}, " +
                        $"itemId: {cell.ItemId}, " +
                        $"artifactDefinition: {artifact?.DefinitionId ?? "none"}, " +
                        $"artifactSpriteReady: {hasArtifactSprite}");
                }
                else
                {
                    Debug.Log("No tile found");
                }
            }
        }

        private void OnDestroy()
        {
            _disposables.Dispose();
        }

        private bool TryGetTileAtMouse_NoPhysics(Tilemap tilemap, out Vector3Int cellPos)
        {
            var worldPos = _cam.ScreenToWorldPoint(Input.mousePosition);
            worldPos.z = 0f;

            cellPos = _view.wallTileMap.WorldToCell(worldPos);
            var tile = tilemap.GetTile(cellPos);

            return tile != null;
        }
    }
}
