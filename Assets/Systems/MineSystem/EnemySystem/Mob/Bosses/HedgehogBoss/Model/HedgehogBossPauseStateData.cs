namespace Systems.MineSystem.EnemySystem.Mob.HedgehogBoss.Model
{
    /// <summary>
    /// Exact physics and presentation snapshot captured for a modal pause.
    /// Mine-only pause never reaches lair actors; this is exclusively for a
    /// global pause which must resume a charge and its animation precisely.
    /// </summary>
    public sealed class HedgehogBossPauseStateData
    {
        public bool HasSnapshot;
        public bool MovementWasPlaying;
        public bool BodyWasSimulated;
        public UnityEngine.Vector2 Velocity;
        public float AngularVelocity;
        public float AnimatorSpeed;
        public bool DamageWasEnabled;

        public void Clear()
        {
            HasSnapshot = false;
            MovementWasPlaying = false;
            BodyWasSimulated = false;
            Velocity = UnityEngine.Vector2.zero;
            AngularVelocity = 0f;
            AnimatorSpeed = 1f;
            DamageWasEnabled = false;
        }
    }
}
