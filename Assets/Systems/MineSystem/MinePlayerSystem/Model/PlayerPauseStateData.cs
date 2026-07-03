using UnityEngine;

namespace Systems.MineSystem.MinePlayerSystem.Model
{
    public sealed class PlayerPauseStateData
    {
        public bool IsPaused { get; set; }
        public bool HasSnapshot { get; set; }
        public bool BodyWasSimulated { get; set; }
        public bool PlayerMapWasEnabled { get; set; }
        public bool DamageWasEnabled { get; set; }
        public bool AutoMovementWasPlaying { get; set; }
        public float GravityScale { get; set; }
        public float AngularVelocity { get; set; }
        public float AnimatorSpeed { get; set; }
        public Vector2 Velocity { get; set; }
        public Vector2 MovementInput { get; set; }

        public void ClearSnapshot() => HasSnapshot = false;
    }
}
