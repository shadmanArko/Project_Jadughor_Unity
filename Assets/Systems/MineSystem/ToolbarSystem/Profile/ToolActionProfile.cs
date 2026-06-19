using Systems.MineSystem.ToolbarSystem.Enum;
using UnityEngine;

namespace Systems.MineSystem.ToolbarSystem.Profile
{
    [CreateAssetMenu(fileName = "ToolActionProfile", menuName = "Toolbar Actions/Tool Profile")]
    public sealed class ToolActionProfile : EquippableActionProfile
    {
        [Min(1)]
        [SerializeField] private int wallDamage = 40;

        public override ItemActionKind ActionKind => ItemActionKind.Tool;
        public int WallDamage => Mathf.Max(1, wallDamage);
    }
}
