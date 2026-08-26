using Systems.MineSystem.MinePlayerSystem.Config;
using Systems.MineSystem.MinePlayerSystem.View;
using UnityEngine;

namespace Systems.MineSystem.BossLairSystem.Service
{
    /// <summary>
    /// Resolves a safe standing position from an authored spawn anchor by
    /// probing for the floor beneath it.
    /// </summary>
    /// <remarks>
    /// This exists because an anchor left floating above the floor is lethal,
    /// not merely untidy: fall damage tiers reach <b>100 damage at 8 cells</b>
    /// against 100 max health, and <c>PlayerDamageService</c> explicitly bypasses
    /// invincibility for fall damage. An 11-cell drop from a stale anchor is an
    /// instant kill on the landing frame.
    /// <para>
    /// The anchor keeps full control of the spawn <i>column</i> — only the height
    /// is corrected — so designers still position spawns by moving the Transform.
    /// </para>
    /// </remarks>
    public sealed class BossLairSpawnService
    {
        /// <summary>
        /// Small lift so the player does not spawn exactly touching the floor.
        /// Physics2D's default contact offset is 0.01, so resting precisely on the
        /// surface starts inside the contact band and gets pushed. The resulting
        /// drop is far below the 2-cell safe fall distance.
        /// </summary>
        private const float GroundClearance = 0.01f;

        private readonly PlayerView _playerView;
        private readonly MinePlayerDataConfig _playerConfig;
        private readonly RaycastHit2D[] _hits = new RaycastHit2D[8];

        public BossLairSpawnService(
            PlayerView playerView,
            MinePlayerDataConfig playerConfig)
        {
            _playerView = playerView;
            _playerConfig = playerConfig;
        }

        /// <summary>
        /// Probes straight down from <paramref name="anchorPosition"/> for the
        /// arena floor and returns a position where the player stands on it.
        /// Returns false when no floor is found within the probe distance, so the
        /// caller can refuse to drop the player into a void.
        /// </summary>
        public bool TryResolveGroundedPosition(
            Vector2 anchorPosition,
            float maxProbeDistance,
            out Vector2 groundedPosition,
            out float dropDistance)
        {
            groundedPosition = anchorPosition;
            dropDistance = 0f;

            var filter = new ContactFilter2D
            {
                useLayerMask = true,
                layerMask = _playerConfig.wallLayerMask,
                useTriggers = false
            };

            var hitCount = Physics2D.Raycast(
                anchorPosition, Vector2.down, filter, _hits, maxProbeDistance);
            if (hitCount <= 0)
                return false;

            var closest = _hits[0];
            for (var i = 1; i < hitCount; i++)
            {
                if (_hits[i].distance < closest.distance)
                    closest = _hits[i];
            }

            // Lift by the player's own half-height so the capsule rests on the
            // surface rather than intersecting it.
            var halfHeight = _playerView.PlayerCollider != null
                ? _playerView.PlayerCollider.bounds.extents.y
                : 0f;
            var offsetToCentre = _playerView.PlayerCollider != null
                ? (Vector2)_playerView.PlayerCollider.bounds.center -
                  (Vector2)_playerView.transform.position
                : Vector2.zero;

            groundedPosition = new Vector2(
                anchorPosition.x,
                closest.point.y + halfHeight - offsetToCentre.y + GroundClearance);
            dropDistance = closest.distance;
            return true;
        }

        /// <summary>
        /// True when the anchor sits far enough above the floor that landing
        /// would deal fall damage, so the caller can warn the designer even
        /// though the spawn itself has been corrected.
        /// </summary>
        public bool IsDropUnsafe(float dropDistance, float cellWorldSize)
        {
            if (cellWorldSize <= 0f)
                return false;
            return dropDistance / cellWorldSize > _playerConfig.safeFallCells;
        }
    }
}
