using Systems.MineSystem.ToolbarSystem.Profile;
using UnityEngine;

namespace Systems.MineSystem.ToolbarSystem.Items.Prefabs.Placeables.Elevator.Script
{
    [CreateAssetMenu(fileName = "ElevatorActionProfile", menuName = "Toolbar Actions/Elevator Profile")]
    public sealed class ElevatorActionProfile : PlaceableActionProfile
    {
        [SerializeField] private ElevatorPlaceableKind kind;
        [SerializeField] private ElevatorConfig config;

        public ElevatorPlaceableKind Kind => kind;
        public ElevatorConfig Config => config;
    }
}
