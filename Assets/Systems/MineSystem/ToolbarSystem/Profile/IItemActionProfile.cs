using Systems.MineSystem.ToolbarSystem.Enum;

namespace Systems.MineSystem.ToolbarSystem.Profile
{
    public interface IItemActionProfile
    {
        string ItemType { get; }
        string ItemCategory { get; }
        string ItemVariant { get; }
        ItemActionKind ActionKind { get; }
    }
}
