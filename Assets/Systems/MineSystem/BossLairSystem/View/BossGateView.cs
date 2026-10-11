using UnityEngine;

namespace Systems.MineSystem.BossLairSystem.View
{
    /// <summary>
    /// The gate placed in the mine that leads to a boss lair. One prefab per
    /// boss, so the gate art tells the player which boss waits behind it.
    /// </summary>
    public sealed class BossGateView : MonoBehaviour
    {
        [Header("Presentation")]
        [Tooltip("Optional renderer used for tinting or state changes.")]
        public SpriteRenderer spriteRenderer;

        [Header("Anchors")]
        [Tooltip(
            "Optional point the player walks to before the transition starts. " +
            "Falls back to this object's own position when empty.")]
        public Transform approachAnchor;

        public Vector2 ApproachPosition =>
            approachAnchor != null
                ? (Vector2)approachAnchor.position
                : (Vector2)transform.position;
    }
}
