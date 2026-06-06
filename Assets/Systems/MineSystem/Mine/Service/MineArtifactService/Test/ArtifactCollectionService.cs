using System;
using System.Linq;
using Systems.MineSystem.InventorySystem.Model;
using Systems.MineSystem.Mine.Model;
using UniRx;

namespace Systems.MineSystem.Mine.Service.MineArtifactService.Test
{
    public sealed class ArtifactCollectionService : IDisposable
    {
        private readonly ArtifactInventoryModel _inventory;
        private readonly Subject<Artifact> _artifactCollected = new();
        private readonly Subject<string> _artifactRemovedFromCell = new();

        public IObservable<Artifact> ArtifactCollected => _artifactCollected;
        public IObservable<string> ArtifactRemovedFromCell => _artifactRemovedFromCell;

        public ArtifactCollectionService(ArtifactInventoryModel inventory)
        {
            _inventory = inventory;
        }

        public bool TryCollectByCell(MineData mineData, string cellId)
        {
            var placement = mineData?.GetArtifactPlacement(cellId);
            return placement != null &&
                   TryCollect(mineData, placement.ArtifactInstanceId);
        }

        public bool TryCollect(MineData mineData, string artifactInstanceId)
        {
            if (mineData?.Artifacts == null || string.IsNullOrEmpty(artifactInstanceId))
                return false;

            var artifact = mineData.Artifacts.FirstOrDefault(item => item.Id == artifactInstanceId);
            if (artifact == null || !_inventory.TryAdd(artifact))
                return false;

            var placement = mineData.ArtifactPlacements?
                .FirstOrDefault(item => item.ArtifactInstanceId == artifactInstanceId);

            if (placement != null)
            {
                var vacatedCellId = placement.CellId;
                var cell = mineData.Cells?
                    .FirstOrDefault(item => item.Id == placement.CellId);
                if (cell != null)
                {
                    cell.HasArtifact = false;
                    cell.ItemId = null;
                }

                mineData.ArtifactPlacements.Remove(placement);
                _artifactRemovedFromCell.OnNext(vacatedCellId);
            }

            mineData.Artifacts.Remove(artifact);
            mineData.InitializeLookupCache();
            _artifactCollected.OnNext(artifact);
            return true;
        }

        public void Dispose()
        {
            _artifactCollected.Dispose();
            _artifactRemovedFromCell.Dispose();
        }
    }
}
