using System;
using System.Collections.Generic;
using System.Linq;
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

        public void InitializeLookupCache()
        {
            if (Cells == null) return;
            
            _cellLookup = new Dictionary<Vector3Int, Cell>(Cells.Count);
            foreach (var cell in Cells)
            {
                _cellLookup[new Vector3Int(cell.Position.X, cell.Position.Y, 0)] = cell;
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
    }
}
