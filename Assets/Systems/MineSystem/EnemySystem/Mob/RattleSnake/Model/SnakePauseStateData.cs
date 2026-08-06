using UnityEngine;

namespace Systems.MineSystem.EnemySystem.Mob.RattleSnake.Model
{
    public sealed class SnakePauseStateData
    {
        public bool HasSnapshot;
        public bool BodyWasSimulated;
        public Vector2 Velocity;
        public float AngularVelocity;
        public float AnimatorSpeed;
        public bool DamageWasEnabled;

        public void Clear()
        {
            HasSnapshot = false;
            BodyWasSimulated = false;
            Velocity = Vector2.zero;
            AngularVelocity = 0f;
            AnimatorSpeed = 1f;
            DamageWasEnabled = false;
        }
    }
}
