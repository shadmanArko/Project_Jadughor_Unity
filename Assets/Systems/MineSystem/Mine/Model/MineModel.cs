using System;
using System.Collections.Generic;
using System.Linq;
using Systems.MineSystem.Mine.Enum;
using Systems.MineSystem.Mine.Service.VisualizerService;
using Systems.MineSystem.MinePlayerSystem.Scriptable;
using UniRx;
using UnityEngine;
using Zenject;

namespace Systems.MineSystem.Mine.Model
{
    [Serializable]
    public class MineModel : IInitializable, IDisposable
    {
        private CompositeDisposable _disposable;
        private MinePlayerScriptable _playerScriptable;
        
        private MineWallVisualizerService _wallVisualizerService;
        private SpecialBackdropVisualizerService _specialBackdropVisualizerService;
        private MineCellCrackVisualizerService _cellCrackVisualizerService;
        
        private ReactiveProperty<MineData> _mineData = new();
        public IReadOnlyReactiveProperty<MineData> MineData => _mineData;

        private Subject<Cell> _onCellModified = new();
        public IObservable<Cell> OnCellModified => _onCellModified;
        
        private Dictionary<Vector3Int, BrokenEdges> _adjacentBrokenEdges;

        public MineModel(
            MinePlayerScriptable playerScriptable, 
            MineWallVisualizerService wallVisualizerService, 
            MineCellCrackVisualizerService cellCrackVisualizerService, 
            SpecialBackdropVisualizerService specialBackdropVisualizerService)
        {
            _playerScriptable = playerScriptable;
            _wallVisualizerService = wallVisualizerService;
            _cellCrackVisualizerService = cellCrackVisualizerService;
            _specialBackdropVisualizerService = specialBackdropVisualizerService;
        }
        
        public void Initialize()
        {
            _disposable = new CompositeDisposable();
            _adjacentBrokenEdges =  new Dictionary<Vector3Int, BrokenEdges>
            {
                [Vector3Int.up] = BrokenEdges.Bottom,
                [Vector3Int.right] = BrokenEdges.Left,
                [Vector3Int.down] = BrokenEdges.Top,
                [Vector3Int.left] = BrokenEdges.Right,
                [new Vector3Int(-1,1,0)] = BrokenEdges.BottomRightCorner,
                [new Vector3Int(1,1,0)] = BrokenEdges.BottomLeftCorner,
                [new Vector3Int(-1,-1,0)] = BrokenEdges.TopRightCorner,
                [new Vector3Int(1,-1,0)] = BrokenEdges.TopLeftCorner
            };
            
            SubscribeToProperties();
        }

        private void SubscribeToProperties()
        {
            OnCellModified.Subscribe(_cellCrackVisualizerService.UpdateCellWallCrack).AddTo(_disposable);
            OnCellModified.Subscribe(_wallVisualizerService.UpdateCellWall).AddTo(_disposable);
        }

        public void SetMineData(MineData mineData)
        {
            // mineData?.InitializeLookupCache();
            _mineData.Value = mineData;
            UpdateAllCellsBrokenEdges();
        }

        #region Autotiling Logic

        private void UpdateAllCellsBrokenEdges()
        {
            if (_mineData.Value == null || _mineData.Value.Cells == null) return;
            
            foreach (var cell in _mineData.Value.Cells)
            {
                if (cell.IsBroken || cell.IsBlank) continue;
                cell.BrokenSides = CalculateBrokenEdges(cell.GetPosition());
            }
        }

        private bool IsEmpty(Vector3Int pos)
        {
            var cell = _mineData.Value.GetCell(pos);
            if (cell == null) return false;
            return cell.IsBroken || cell.IsBlank; 
        }

        public BrokenEdges CalculateBrokenEdges(Vector3Int pos)
        {
            BrokenEdges edges = BrokenEdges.Intact;

            bool t = IsEmpty(pos + Vector3Int.up);
            bool r = IsEmpty(pos + Vector3Int.right);
            bool b = IsEmpty(pos + Vector3Int.down);
            bool l = IsEmpty(pos + Vector3Int.left);

            if (t) edges |= BrokenEdges.Top;
            if (r) edges |= BrokenEdges.Right;
            if (b) edges |= BrokenEdges.Bottom;
            if (l) edges |= BrokenEdges.Left;

            // If all 4 straight edges are broken, it's an isolated block. Return -1 to match the "All" entry.
            if (t && r && b && l)
            {
                return (BrokenEdges)(-1);
            }

            if (!t && !l && IsEmpty(pos + new Vector3Int(-1, 1, 0))) edges |= BrokenEdges.TopLeftCorner;
            if (!t && !r && IsEmpty(pos + new Vector3Int(1, 1, 0))) edges |= BrokenEdges.TopRightCorner;
            if (!b && !r && IsEmpty(pos + new Vector3Int(1, -1, 0))) edges |= BrokenEdges.BottomRightCorner;
            if (!b && !l && IsEmpty(pos + new Vector3Int(-1, -1, 0))) edges |= BrokenEdges.BottomLeftCorner;

            bool hasSide = (edges & (BrokenEdges.Top | BrokenEdges.Right | BrokenEdges.Bottom | BrokenEdges.Left)) != 0;
            if (hasSide)
            {
                edges &= ~(BrokenEdges.TopLeftCorner | BrokenEdges.TopRightCorner | BrokenEdges.BottomRightCorner | BrokenEdges.BottomLeftCorner);
            }

            return edges;
        }

        #endregion
        
        public void GenerateMineFromData()
        {
            var mineData = MineData.Value;
            _wallVisualizerService.GenerateMineFromData(mineData);
            _specialBackdropVisualizerService.SetSpecialBackdrops(mineData.SpecialBackdropDatas, _playerScriptable.region, _playerScriptable.site);
        }

        public void HitCell(Vector3Int cellPos)
        {
            var cell = _mineData.Value.GetCell(cellPos);
            
            if (cell == null)
            {
                Debug.LogWarning($"Cell not available: {cellPos}");
                return;
            }

            cell.HitPoint -= _playerScriptable.playerData.pickAxeStrength.Value;
            
            cell.IsBroken = cell.HitPoint <= 0;
            _onCellModified.OnNext(cell);

            if (cell.IsBroken)
            {
                HashSet<Cell> revealedCells = new HashSet<Cell>();
                
                cell.IsRevealed = true;
                revealedCells.Add(cell);

                RevealAdjacentCells(cellPos, revealedCells);

                if (!string.IsNullOrEmpty(cell.CaveId))
                    HandleCaveCell(cell, revealedCells);

                foreach (var c in revealedCells)
                {
                    if(!c.IsBreakable) continue;
                    c.BrokenSides = CalculateBrokenEdges(c.GetPosition());
                    _onCellModified.OnNext(c);
                }
            }
            
            //TODO: make resource, artifact null after spawning those as items
        }

        private void RevealAdjacentCells(Vector3Int cellPos, HashSet<Cell> revealedCells)
        {
            foreach (var adjacentCell in _adjacentBrokenEdges.Keys.Select(offset => 
                         cellPos + offset).Select(adjacentCellPos => 
                         _mineData.Value.GetCell(adjacentCellPos)).Where(adjacentCell => 
                         adjacentCell != null))
            {
                if (!adjacentCell.IsRevealed)
                {
                    adjacentCell.IsRevealed = true;
                    revealedCells.Add(adjacentCell);
                    
                    if (!string.IsNullOrEmpty(adjacentCell.CaveId))
                        HandleCaveCell(adjacentCell, revealedCells);
                }
                else
                    revealedCells.Add(adjacentCell);
            }
        }

        private void HandleCaveCell(Cell cell, HashSet<Cell> revealedCells)
        {
            var cave = MineData.Value.Caves.FirstOrDefault(c => c.Id == cell.CaveId);
            if (cave == null)
            {
                Debug.LogError($"Fatal Error: Cave ID mismatch: {cell.CaveId}");
                return;
            }
            
            if (cave.IsRevealed) return;
            
            // First pass: mark all cave cells as revealed and broken
            foreach (var position in cave.CellPositions)
            {
                var caveCell = MineData.Value.GetCell(position);
                if (caveCell == null)
                {
                    Debug.LogError($"Could not find cave position: {position}");
                    continue;
                }
                
                caveCell.IsRevealed = true;
                caveCell.IsBroken = true;
                revealedCells.Add(caveCell);
            }

            // Second pass: reveal adjacent cells of the cave boundaries
            foreach (var position in cave.CellPositions)
            {
                var caveCell = MineData.Value.GetCell(position);
                if (caveCell == null) continue;

                foreach (var offset in _adjacentBrokenEdges.Keys)
                {
                    var adjPos = caveCell.GetPosition() + offset;
                    var adjCell = _mineData.Value.GetCell(adjPos);
                    
                    if (adjCell == null) continue;
                    
                    if (!adjCell.IsRevealed)
                    {
                        adjCell.IsRevealed = true;
                        revealedCells.Add(adjCell);
                        
                        if (!string.IsNullOrEmpty(adjCell.CaveId) && adjCell.CaveId != cave.Id)
                        {
                            HandleCaveCell(adjCell, revealedCells);
                        }
                    }
                    else
                    {
                        revealedCells.Add(adjCell);
                    }
                }
            }
            
            cave.IsRevealed = true;
        }

        public void Dispose()
        {
            _mineData.Dispose();
            _onCellModified?.Dispose();
            _disposable?.Dispose();
        }
    }
}