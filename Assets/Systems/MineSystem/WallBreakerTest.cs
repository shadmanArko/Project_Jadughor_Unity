using System;
using Systems.MineSystem.Mine.Controller;
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
                if (TryGetTileAtMouse_NoPhysics(out var worldPos,
                        out var cellPos,
                        out var tile))
                {
                    Debug.Log($"Clicked tile at {cellPos}, tile: {tile.name}");
                    _mineController.HitWall(cellPos);
                }
                else
                {
                    Debug.Log("No tile found");
                }
            }
        }

        private bool TryGetTileAtMouse_NoPhysics(out Vector3 worldPos, out Vector3Int cellPos, out TileBase tile)
        {
            worldPos = _cam.ScreenToWorldPoint(Input.mousePosition);
            worldPos.z = 0f;

            cellPos = _view.wallTileMap.WorldToCell(worldPos);
            tile = _view.wallTileMap.GetTile(cellPos);

            return tile != null;
        }
    }
}