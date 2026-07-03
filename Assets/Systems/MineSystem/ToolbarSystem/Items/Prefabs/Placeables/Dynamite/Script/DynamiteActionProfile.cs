using Systems.MineSystem.ToolbarSystem.Profile;
using UnityEngine;

namespace Systems.MineSystem.ToolbarSystem.Items.Prefabs.Placeables.Dynamite.Script
{
    [CreateAssetMenu(
        fileName = "DynamiteActionProfile",
        menuName = "Toolbar Actions/Dynamite Profile")]
    public sealed class DynamiteActionProfile : PlaceableActionProfile
    {
        [SerializeField] private DynamiteConfig config;
        public DynamiteConfig Config => config;
    }
}
