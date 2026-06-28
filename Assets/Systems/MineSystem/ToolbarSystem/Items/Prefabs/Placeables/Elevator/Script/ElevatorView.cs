using Systems.MineSystem.ToolbarSystem.View;
using UnityEngine;

namespace Systems.MineSystem.ToolbarSystem.Items.Prefabs.Placeables.Elevator.Script
{
    public sealed class ElevatorView : PlaceableDamageView
    {
        [SerializeField] private SpriteRenderer spriteRenderer;

        public SpriteRenderer SpriteRenderer => spriteRenderer;

        public void Configure(ElevatorPlaceableKind kind, ElevatorConfig config)
        {
            spriteRenderer = spriteRenderer != null
                ? spriteRenderer
                : GetComponentInChildren<SpriteRenderer>(true);

            if (spriteRenderer == null || config == null)
                return;

            spriteRenderer.sortingOrder = kind == ElevatorPlaceableKind.Lift
                ? config.LiftSortingOrder
                : config.ShaftSortingOrder;
        }
    }
}
