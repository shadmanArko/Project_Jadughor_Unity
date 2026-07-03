using UnityEngine;

namespace Systems.MineSystem.CollectableSystem.Model
{
    public sealed class CollectablePauseStateData
    {
        public bool HasSnapshot { get; set; }
        public bool BodyWasSimulated { get; set; }
        public bool TriggerWasEnabled { get; set; }
        public float GravityScale { get; set; }
        public float AngularVelocity { get; set; }
        public float CollectorScanRemaining { get; set; }
        public float AttractionDelayRemaining { get; set; }
        public Vector2 Velocity { get; set; }
    }
}
