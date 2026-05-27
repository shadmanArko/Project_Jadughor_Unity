using System;
using Systems.MineSystem.Mine.Controller;
using Systems.MineSystem.Mine.Model;
using Systems.MineSystem.Mine.View;
using Systems.Utilities.Injector;
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
        private Tilemap _targetTilemap;

        private InputSystem_Actions _inputMaster;

        private void Start()
        {
            ManualInjector.InjectDependencies(this);
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
                    // Debug.Log($"Cell Position: {cell.Position}, IsBroken: {cell.IsBroken}, IsRevealed: {cell.IsRevealed}, IsBreakable: {cell.IsBreakable}, CaveId: {cell.CaveId}");
                    Debug.Log($"Cell Position: {cell.Position}, hasResource: {cell.HasResource}, resourceId: {cell.ItemId}");
                }
                else
                {
                    Debug.Log("No tile found");
                }
            }
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