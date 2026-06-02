using System;
using System.Collections.Generic;
using System.Linq;
using Systems.MineSystem.InventorySystem.Model;
using Systems.MineSystem.Mine.Service.MineResourceService.Model;
using UnityEngine;

namespace Systems.MineSystem.Mine.Model
{
    [Serializable]
    public class MineData
    {
        /// <summary>
        /// Increment this when the schema changes to support save-file migration.
        /// </summary>
        public int Version { get; set; } = 1;

        public int CellSize { get; set; }
        public int GridWidth { get; set; }
        public int GridHeight { get; set; }

        public List<Cell> Cells { get; set; }
        public List<Resource> Resources { get; set; }
        public List<Artifact> Artifacts { get; set; }
        public List<Cave> Caves { get; set; }
        public List<WallPlaceable> WallPlaceables { get; set; }
        public List<CellPlaceable> CellPlaceables { get; set; }
        public List<VineData> VineDatas { get; set; }
        public List<SpecialBackdropData> SpecialBackdropDatas { get; set; }

        [NonSerialized]
        private Dictionary<Vector3Int, Cell> _cellLookup;

        [NonSerialized]
        private Dictionary<string, Resource> _resourceByCellId;

        [NonSerialized]
        private Dictionary<string, Artifact> _artifactByCellId;

        [NonSerialized]
        private Dictionary<string, WallPlaceable> _wallPlaceableByCellId;

        public void InitializeLookupCache()
        {
            if (Cells == null) return;
            
            _cellLookup = new Dictionary<Vector3Int, Cell>(Cells.Count);
            foreach (var cell in Cells)
            {
                _cellLookup[new Vector3Int(cell.Position.X, cell.Position.Y, 0)] = cell;
            }

            _resourceByCellId = new Dictionary<string, Resource>();
            if (Resources != null)
            {
                foreach (var r in Resources)
                {
                    if (!string.IsNullOrEmpty(r.CellId)) _resourceByCellId[r.CellId] = r;
                }
            }

            _artifactByCellId = new Dictionary<string, Artifact>();
            if (Artifacts != null)
            {
                foreach (var a in Artifacts)
                {
                    if (!string.IsNullOrEmpty(a.CellId)) _artifactByCellId[a.CellId] = a;
                }
            }

            _wallPlaceableByCellId = new Dictionary<string, WallPlaceable>();
            if (WallPlaceables != null)
            {
                foreach (var wp in WallPlaceables)
                {
                    if (wp.OccupiedCellIds != null)
                    {
                        foreach (var id in wp.OccupiedCellIds)
                        {
                            _wallPlaceableByCellId[id] = wp;
                        }
                    }
                }
            }
        }

        public Cell GetCell(GridPosition position) => GetCell(new Vector3Int(position.X, position.Y, 0));

        public Cell GetCell(Vector3Int position)
        {
            if (_cellLookup != null && _cellLookup.TryGetValue(position, out var cell))
            {
                return cell;
            }
            // Fallback in case cache isn't initialized yet
            return Cells?.FirstOrDefault(c => c.Position == position);
        }

        public Resource GetResource(string cellId) => _resourceByCellId != null && _resourceByCellId.TryGetValue(cellId, out var r) ? r : null;
        public Artifact GetArtifact(string cellId) => _artifactByCellId != null && _artifactByCellId.TryGetValue(cellId, out var a) ? a : null;
        public WallPlaceable GetWallPlaceable(string cellId) => _wallPlaceableByCellId != null && _wallPlaceableByCellId.TryGetValue(cellId, out var wp) ? wp : null;
    }
}
