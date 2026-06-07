using Systems.MineSystem.InventorySystem.Model;
using Systems.MineSystem.Mine.Model;
using Systems.MineSystem.Mine.Service.MineResourceService.Model;

namespace Systems.MineSystem.InventorySystem.Service
{
    public sealed class InventoryItemDescriptionService
    {
        public string Build(Item item)
        {
            if (item == null)
                return string.Empty;

            var header = $"{item.Name}\n{item.Type} / {item.Category} / {item.Variant}";
            return item switch
            {
                Artifact artifact =>
                    $"{header}\nMaterial: {artifact.Material}\n" +
                    $"Condition: {artifact.Condition}\nRarity: {artifact.Rarity}",
                Resource resource =>
                    $"{header}\nStackable resource: {resource.IsStackable}",
                CellPlaceable placeable =>
                    $"{header}\nCell placeable\nScene: {placeable.ScenePath}",
                WallPlaceable placeable =>
                    $"{header}\nWall placeable\nScene: {placeable.ScenePath}",
                _ => header
            };
        }
    }
}
