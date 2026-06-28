using UnityEngine;

namespace Systems.MineSystem.ToolbarSystem.Items.Prefabs.Placeables.Elevator.Script
{
    [CreateAssetMenu(fileName = "ElevatorConfig", menuName = "Toolbar Actions/Elevator Config")]
    public sealed class ElevatorConfig : ScriptableObject
    {
        [Min(0.01f)]
        [SerializeField] private float moveDurationSeconds = 0.22f;
        [SerializeField] private Vector2 riderOffset = new(0f, 0.05f);
        [SerializeField] private Vector2 exitOffset = new(0f, 0.05f);
        [SerializeField] private int liftSortingOrder = 1;
        [SerializeField] private int shaftSortingOrder;

        public float MoveDurationSeconds => Mathf.Max(0.01f, moveDurationSeconds);
        public Vector2 RiderOffset => riderOffset;
        public Vector2 ExitOffset => exitOffset;
        public int LiftSortingOrder => liftSortingOrder;
        public int ShaftSortingOrder => shaftSortingOrder;
    }
}
