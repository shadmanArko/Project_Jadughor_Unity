using System;
using System.Collections.Generic;
using System.Linq;
using Systems.MineSystem.InventorySystem.Model;
using Systems.MineSystem.Mine.Service.MineArtifactService.Model;
using Systems.MineSystem.Mine.Service.MineArtifactService.Test;
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
        public int Version { get; set; } = 4;

        public int CellSize { get; set; }
        public int GridWidth { get; set; }
        public int GridHeight { get; set; }

        public List<Cell> Cells { get; set; }
        public List<Resource> Resources { get; set; }
        public List<Artifact> Artifacts { get; set; }
        public List<ArtifactWorldPlacementData> ArtifactPlacements { get; set; }
        public List<Cave> Caves { get; set; }
        public List<WallPlaceable> WallPlaceables { get; set; }
        public List<CellPlaceable> CellPlaceables { get; set; }
        public List<VineData> VineDatas { get; set; }
        public List<SpecialBackdropData> SpecialBackdropDatas { get; set; }

        [NonSerialized]
        private Dictionary<Vector3Int, Cell> _cellLookup;

        [NonSerialized]
        private Dictionary<string, Cell> _cellById;

        [NonSerialized]
        private Dictionary<string, Resource> _resourceByCellId;

        [NonSerialized]
        private Dictionary<string, Artifact> _artifactByCellId;

        [NonSerialized]
        private Dictionary<string, ArtifactWorldPlacementData> _artifactPlacementByCellId;

        [NonSerialized]
        private Dictionary<string, WallPlaceable> _wallPlaceableByCellId;

        [NonSerialized]
        private Dictionary<string, CellPlaceable> _cellPlaceableByCellId;

        [NonSerialized]
        private Dictionary<string, VineData> _vineByCellId;

        public void InitializeLookupCache()
        {
            if (Cells == null) return;
            
            _cellLookup = new Dictionary<Vector3Int, Cell>(Cells.Count);
            _cellById = new Dictionary<string, Cell>(Cells.Count);
            foreach (var cell in Cells)
            {
                cell.HasVine = false;
                _cellLookup[new Vector3Int(cell.Position.X, cell.Position.Y, 0)] = cell;
                if (!string.IsNullOrEmpty(cell.Id))
                    _cellById[cell.Id] = cell;
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
            _artifactPlacementByCellId = new Dictionary<string, ArtifactWorldPlacementData>();
            if (Artifacts != null && ArtifactPlacements != null)
            {
                var artifactById = Artifacts
                    .Where(artifact => !string.IsNullOrEmpty(artifact.Id))
                    .ToDictionary(artifact => artifact.Id);

                foreach (var placement in ArtifactPlacements)
                {
                    if (string.IsNullOrEmpty(placement.CellId))
                        continue;

                    _artifactPlacementByCellId[placement.CellId] = placement;
                    if (artifactById.TryGetValue(placement.ArtifactInstanceId, out var artifact))
                        _artifactByCellId[placement.CellId] = artifact;
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

            _cellPlaceableByCellId = new Dictionary<string, CellPlaceable>();
            if (CellPlaceables != null)
            {
                foreach (var placeable in CellPlaceables)
                {
                    if (!string.IsNullOrEmpty(placeable.OccupiedCellId))
                        _cellPlaceableByCellId[placeable.OccupiedCellId] = placeable;
                }
            }

            _vineByCellId = new Dictionary<string, VineData>();
            if (VineDatas != null)
            {
                foreach (var vine in VineDatas)
                {
                    if (vine?.VineCellIds == null)
                        continue;

                    foreach (var cellId in vine.VineCellIds)
                    {
                        if (string.IsNullOrEmpty(cellId))
                            continue;

                        _vineByCellId[cellId] = vine;
                        if (_cellById.TryGetValue(cellId, out var cell))
                            cell.HasVine = true;
                    }
                }
            }
        }

        public Cell GetCell(GridPosition position) => GetCell(new Vector3Int(position.X, position.Y, 0));
        public Cell GetCellById(string id) =>
            !string.IsNullOrEmpty(id) &&
            _cellById != null &&
            _cellById.TryGetValue(id, out var cell)
                ? cell
                : Cells?.FirstOrDefault(candidate => candidate.Id == id);

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
        public ArtifactWorldPlacementData GetArtifactPlacement(string cellId) =>
            _artifactPlacementByCellId != null &&
            _artifactPlacementByCellId.TryGetValue(cellId, out var placement)
                ? placement
                : null;
        public WallPlaceable GetWallPlaceable(string cellId) => _wallPlaceableByCellId != null && _wallPlaceableByCellId.TryGetValue(cellId, out var wp) ? wp : null;
        public CellPlaceable GetCellPlaceable(string cellId) =>
            _cellPlaceableByCellId != null &&
            _cellPlaceableByCellId.TryGetValue(cellId, out var placeable)
                ? placeable
                : null;
        public VineData GetVine(string cellId) =>
            _vineByCellId != null &&
            _vineByCellId.TryGetValue(cellId, out var vine)
                ? vine
                : null;
        public bool HasVine(string cellId) => GetVine(cellId) != null;

        public void RegisterCellPlaceable(
            CellPlaceable placeable,
            IEnumerable<string> occupiedCellIds)
        {
            CellPlaceables ??= new List<CellPlaceable>();
            CellPlaceables.Add(placeable);
            _cellPlaceableByCellId ??=
                new Dictionary<string, CellPlaceable>();
            foreach (var cellId in occupiedCellIds)
                _cellPlaceableByCellId[cellId] = placeable;
        }

        public void RegisterWallPlaceable(WallPlaceable placeable)
        {
            WallPlaceables ??= new List<WallPlaceable>();
            WallPlaceables.Add(placeable);
            _wallPlaceableByCellId ??=
                new Dictionary<string, WallPlaceable>();
            foreach (var cellId in placeable.OccupiedCellIds)
                _wallPlaceableByCellId[cellId] = placeable;
        }

        public void UnregisterCellPlaceable(string instanceId)
        {
            CellPlaceables?.RemoveAll(value => value.Id == instanceId);
            RemoveLookupValues(_cellPlaceableByCellId, instanceId);
        }

        public void UnregisterWallPlaceable(string instanceId)
        {
            WallPlaceables?.RemoveAll(value => value.Id == instanceId);
            RemoveLookupValues(_wallPlaceableByCellId, instanceId);
        }

        private static void RemoveLookupValues<T>(
            Dictionary<string, T> lookup,
            string instanceId) where T : Placeable
        {
            if (lookup == null)
                return;

            var keys = lookup
                .Where(pair => pair.Value.Id == instanceId)
                .Select(pair => pair.Key)
                .ToArray();
            foreach (var key in keys)
                lookup.Remove(key);
        }
    }
}
