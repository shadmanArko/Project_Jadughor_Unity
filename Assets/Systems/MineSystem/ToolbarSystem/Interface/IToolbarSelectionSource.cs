using Systems.MineSystem.InventorySystem.Model;
using UniRx;

namespace Systems.MineSystem.ToolbarSystem.Interface
{
    public interface IToolbarSelectionSource
    {
        IReadOnlyReactiveProperty<Item> HighlightedItem { get; }
        IReadOnlyReactiveProperty<int> HighlightedSlot { get; }
    }
}
