using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;

namespace Systems.MineSystem.BossLairSystem.View
{
    /// <summary>
    /// Unity surface of a boss lair instance: its own grid, tilemaps, camera
    /// bounds, lighting and anchor points. Deliberately independent of
    /// <c>MineView</c> so the arena shares no state with the mine.
    /// </summary>
    /// <remarks>
    /// The wall and background tilemaps are painted at runtime by
    /// <c>BossLairShellGenerationService</c>; only decoration and the anchors are
    /// authored. Requirements the prefab must still satisfy, because gameplay
    /// code resolves against physics rather than lair data:
    /// <list type="bullet">
    /// <item>The wall tilemap needs a <c>TilemapCollider2D</c> plus a
    /// <c>CompositeCollider2D</c> and a static <c>Rigidbody2D</c> on the
    /// <c>Wall</c> layer, matching the mine, so player grounding, collision and
    /// fall damage work unchanged.</item>
    /// <item>The grid cell size must match the mine grid, because weapon reach
    /// and fall-damage height are derived from the mine's cell size.</item>
    /// <item>The arena light must not be a global light. URP 2D accumulates
    /// global lights on a shared blend style, so a second one would brighten
    /// the mine as well.</item>
    /// </list>
    /// </remarks>
    public sealed class BossLairView : MonoBehaviour
    {
        [Header("Grid")]
        public Grid grid;

        [Header("Tilemaps")]
        public Tilemap backgroundTileMap;
        public Tilemap wallTileMap;
        public Tilemap decorTileMap;

        [Header("Camera")]
        [Tooltip("Bounding shape handed to the Cinemachine confiner in the lair.")]
        public BoxCollider2D cameraBoundaryCollider;

        [Header("Lighting")]
        [Tooltip("Arena light. Must not use Light2D global type.")]
        public Light2D arenaLight;

        [Header("Anchors")]
        [Tooltip("Where the player is placed on entering the arena.")]
        public Transform playerSpawnPoint;
        [Tooltip("Where the player must stand to leave the arena.")]
        public Transform exitAnchor;
        [Tooltip("Where the boss is spawned.")]
        public Transform bossSpawnPoint;

        public void ValidateReferences()
        {
            if (grid == null)
                throw new InvalidOperationException(
                    $"{name} requires a Grid.");
            if (wallTileMap == null)
                throw new InvalidOperationException(
                    $"{name} requires a wall tilemap.");
            if (backgroundTileMap == null)
                throw new InvalidOperationException(
                    $"{name} requires a background tilemap.");
            if (decorTileMap == null)
                throw new InvalidOperationException(
                    $"{name} requires a decor tilemap.");
            if (cameraBoundaryCollider == null)
                throw new InvalidOperationException(
                    $"{name} requires a camera boundary collider.");
            if (playerSpawnPoint == null)
                throw new InvalidOperationException(
                    $"{name} requires a player spawn point.");
            if (exitAnchor == null)
                throw new InvalidOperationException(
                    $"{name} requires an exit anchor.");
            if (bossSpawnPoint == null)
                throw new InvalidOperationException(
                    $"{name} requires a boss spawn point.");
            if (arenaLight != null && arenaLight.lightType == Light2D.LightType.Global)
                throw new InvalidOperationException(
                    $"{name} arena light must not be a global light: URP 2D " +
                    "accumulates global lights, so it would brighten the mine.");
        }

        /// <summary>
        /// Enables or disables the arena's own presentation. The lair is built
        /// during mine generation but stays dormant until the player enters, so
        /// its light does not burn while nobody is there.
        /// </summary>
        public void SetArenaActive(bool active)
        {
            if (arenaLight != null)
                arenaLight.enabled = active;
        }

        /// <summary>
        /// Sizes the camera bounding shape to the arena. Called after the lair
        /// is instantiated so the shape follows the resolved arena size rather
        /// than whatever was authored on the prefab.
        /// </summary>
        public void ApplyCameraBounds(Vector2 worldCenter, Vector2 worldSize)
        {
            var boundaryTransform = cameraBoundaryCollider.transform;
            var localCenter = boundaryTransform.InverseTransformPoint(worldCenter);
            var scale = boundaryTransform.lossyScale;

            cameraBoundaryCollider.offset = localCenter;
            cameraBoundaryCollider.size = new Vector2(
                worldSize.x / Mathf.Max(0.0001f, Mathf.Abs(scale.x)),
                worldSize.y / Mathf.Max(0.0001f, Mathf.Abs(scale.y)));
        }
    }
}
