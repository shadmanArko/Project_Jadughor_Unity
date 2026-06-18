using System;
using Systems.MineSystem.InventorySystem.Model;
using UniRx;
using Zenject;

namespace Systems.MineSystem.Mine.Service.MineArtifactService.Test
{
    public sealed class ArtifactTooltipData
    {
        public string Name { get; }
        public string Description { get; }
        public string Era { get; }
        public string Region { get; }
        public string ObjectClass { get; }
        public string ObjectSize { get; }
        public string Material { get; }
        public string Condition { get; }
        public string Rarity { get; }
        public string ImageLocation { get; }

        public ArtifactTooltipData(
            string name,
            string description,
            ArtifactDefinition definition,
            Artifact artifact)
        {
            Name = name;
            Description = description;
            Era = definition.Era;
            Region = definition.Region;
            ObjectClass = definition.ObjectClass;
            ObjectSize = definition.ObjectSize;
            Material = artifact.Material;
            Condition = artifact.Condition.ToString();
            Rarity = artifact.Rarity.ToString();
            ImageLocation = definition.LargeImageLocation;
        }
    }

    public interface IArtifactTooltipView
    {
        void Show(ArtifactTooltipData data);
        void Hide();
    }

    public sealed class ArtifactTooltipPresenter : IInitializable, IDisposable
    {
        private readonly ArtifactInventoryModel _inventory;
        private readonly IArtifactCatalog _catalog;
        private readonly IArtifactTooltipView _view;
        private readonly CompositeDisposable _disposables = new();

        public ArtifactTooltipPresenter(
            ArtifactInventoryModel inventory,
            IArtifactCatalog catalog,
            IArtifactTooltipView view)
        {
            _inventory = inventory;
            _catalog = catalog;
            _view = view;
        }

        public void Initialize()
        {
            _inventory.HoveredItem
                .Subscribe(ShowHoveredItem)
                .AddTo(_disposables);
        }

        private void ShowHoveredItem(Item item)
        {
            if (item is not Artifact artifact ||
                !_catalog.TryGetDefinition(artifact.DefinitionId, out var definition))
            {
                _view.Hide();
                return;
            }

            _catalog.TryGetDescription(artifact.DefinitionId, out var description);

            var tooltip = new ArtifactTooltipData(
                description?.ArtifactName ?? definition.Object,
                description?.Description ?? string.Empty,
                definition,
                artifact);

            _view.Show(tooltip);
        }

        public void Dispose()
        {
            _disposables.Dispose();
        }
    }
}
