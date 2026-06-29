using System;
using System.Collections.Generic;
using System.Linq;
using Systems.MineSystem.InventorySystem.Model;
using Systems.MineSystem.Mine.Enum;
using Systems.MineSystem.Mine.Service.MineArtifactService.Test;
using Systems.MineSystem.Mine.Service.MineResourceService.Service;
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
        private VineVisualizerService _vineVisualizerService;
        private CellCrackVisualizerService _cellCrackVisualizerService;
        private ResourceVisualizerService _resourceVisualizerService;
        private ArtifactVisualizerService _artifactVisualizerService;
        private CaveVisualizerService _caveVisualizerService;
        
        private ReactiveProperty<MineData> _mineData = new();
        public IReadOnlyReactiveProperty<MineData> MineData => _mineData;

        private Subject<Cell> _onCellModified = new();
        public IObservable<Cell> OnCellModified => _onCellModified;

        private Subject<Artifact> _onArtifactDiscovered = new();
        public IObservable<Artifact> OnArtifactDiscovered => _onArtifactDiscovered;

        private Subject<Cell> _onCellBroken = new();
        public IObservable<Cell> OnCellBroken => _onCellBroken;
        
        private Dictionary<Vector3Int, BrokenEdges> _adjacentBrokenEdges;

        public MineModel(
            MinePlayerScriptable playerScriptable, 
            MineWallVisualizerService wallVisualizerService, 
            CellCrackVisualizerService cellCrackVisualizerService, 
            SpecialBackdropVisualizerService specialBackdropVisualizerService, 
            VineVisualizerService vineVisualizerService,
            ResourceVisualizerService resourceVisualizerService,
            ArtifactVisualizerService artifactVisualizerService,
            CaveVisualizerService caveVisualizerService)
        {
            _playerScriptable = playerScriptable;
            _wallVisualizerService = wallVisualizerService;
            _cellCrackVisualizerService = cellCrackVisualizerService;
            _specialBackdropVisualizerService = specialBackdropVisualizerService;
            _vineVisualizerService = vineVisualizerService;
            _resourceVisualizerService = resourceVisualizerService;
            _artifactVisualizerService = artifactVisualizerService;
            _caveVisualizerService = caveVisualizerService;
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
            OnCellModified.Subscribe(_resourceVisualizerService.UpdateResourceTile).AddTo(_disposable);
            OnCellModified.Subscribe(_artifactVisualizerService.UpdateArtifactTile).AddTo(_disposable);
        }

        public void SetMineData(MineData mineData)
        {
            // mineData?.InitializeLookupCache();
            _mineData.Value = mineData;
            _artifactVisualizerService.SetMineData(mineData);
            _cellCrackVisualizerService.RefreshCellCracks(mineData);
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
            _caveVisualizerService.ResetFormations();
            _wallVisualizerService.GenerateMineFromData(mineData);
            _cellCrackVisualizerService.RefreshCellCracks(mineData);
            _vineVisualizerService.SetVines(mineData.VineDatas, mineData, _playerScriptable.region, _playerScriptable.site);
            _specialBackdropVisualizerService.SetSpecialBackdrops(mineData.SpecialBackdropDatas, _playerScriptable.region, _playerScriptable.site);
        }

        public void HitCell(Vector3Int cellPos)
        {
            TryHitCell(
                cellPos,
                _playerScriptable.playerData.pickAxeStrength.Value);
        }

        public bool TryHitCell(Vector3Int cellPos, int damage)
        {
            if (_mineData.Value == null || damage <= 0)
                return false;

            var cell = _mineData.Value.GetCell(cellPos);
            
            if (cell == null || !cell.IsBreakable || cell.IsBroken)
            {
                return false;
            }

            var wasBroken = cell.IsBroken;
            cell.HitPoint -= damage;
            
            cell.IsBroken = cell.HitPoint <= 0;
            _onCellModified.OnNext(cell);

            if (cell.IsBroken)
            {
                if (!wasBroken)
                {
                    _onCellBroken.OnNext(cell);
                    _caveVisualizerService.HandleRootCellBroken(cell);
                }

                if (!wasBroken && cell.HasArtifact)
                {
                    var artifact = _mineData.Value.GetArtifact(cell.Id);
                    if (artifact != null)
                        _onArtifactDiscovered.OnNext(artifact);
                }

                var revealedCells = new HashSet<Cell>();
                
                cell.IsRevealed = true;
                revealedCells.Add(cell);

                RevealAdjacentCells(cellPos, revealedCells);

                if (!string.IsNullOrEmpty(cell.CaveId))
                    _caveVisualizerService.TryRevealCave(
                        cell,
                        MineData.Value,
                        revealedCells,
                        _adjacentBrokenEdges.Keys);

                foreach (var c in revealedCells)
                {
                    if(!c.IsBreakable) continue;
                    c.BrokenSides = CalculateBrokenEdges(c.GetPosition());
                    _onCellModified.OnNext(c);
                }
            }
            
            //TODO: make resource, artifact null after spawning those as items
            return true;
        }

        public void NotifyCellModified(Cell cell)
        {
            if (cell != null)
                _onCellModified.OnNext(cell);
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
                        _caveVisualizerService.TryRevealCave(
                            adjacentCell,
                            MineData.Value,
                            revealedCells,
                            _adjacentBrokenEdges.Keys);
                }
                else
                    revealedCells.Add(adjacentCell);
            }
        }

        public void Dispose()
        {
            _mineData.Dispose();
            _onCellModified?.Dispose();
            _onArtifactDiscovered?.Dispose();
            _onCellBroken?.Dispose();
            _disposable?.Dispose();
        }
    }
}
