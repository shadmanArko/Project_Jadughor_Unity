using Systems.MineSystem.ToolbarSystem.Profile;
using UnityEngine;

namespace Systems.MineSystem.ToolbarSystem.Items.Prefabs.Placeables.PileDriver.Script
{
    [CreateAssetMenu(
        fileName = "PileDriverActionProfile",
        menuName = "Toolbar Actions/PileDriver Profile")]
    public sealed class PileDriverActionProfile : PlaceableActionProfile
    {
        [Header("Machine Configuration")]
        [SerializeField] private PileDriverConfig config;

        public PileDriverConfig Config => config;
    }
}
