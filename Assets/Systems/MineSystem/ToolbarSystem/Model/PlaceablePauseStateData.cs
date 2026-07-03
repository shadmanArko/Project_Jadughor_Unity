using System.Collections.Generic;
using Systems.MineSystem.ToolbarSystem.Interface;
using UnityEngine;

namespace Systems.MineSystem.ToolbarSystem.Model
{
    public sealed class PlaceablePauseStateData
    {
        private readonly Dictionary<Rigidbody2D,
            (bool Simulated, Vector2 Velocity, float AngularVelocity, float Gravity)>
            _bodies = new();
        private readonly Dictionary<Animator, float> _animators = new();
        private bool _damageEnabled;
        public bool HasSnapshot { get; private set; }

        public void Capture(Transform root, IPlaceableDamageView damageView)
        {
            if (HasSnapshot || root == null) return;
            HasSnapshot = true;
            _damageEnabled = damageView?.DamageEnabled ?? false;
            foreach (var body in root.GetComponentsInChildren<Rigidbody2D>(true))
            {
                _bodies[body] = (body.simulated, body.linearVelocity,
                    body.angularVelocity, body.gravityScale);
                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0f;
                body.simulated = false;
            }
            foreach (var animator in root.GetComponentsInChildren<Animator>(true))
            {
                _animators[animator] = animator.speed;
                animator.speed = 0f;
            }
            damageView?.SetDamageEnabled(false);
        }

        public void Restore(IPlaceableDamageView damageView)
        {
            if (!HasSnapshot) return;
            foreach (var pair in _bodies)
            {
                if (pair.Key == null) continue;
                pair.Key.simulated = pair.Value.Simulated;
                pair.Key.gravityScale = pair.Value.Gravity;
                pair.Key.linearVelocity = pair.Value.Velocity;
                pair.Key.angularVelocity = pair.Value.AngularVelocity;
            }
            foreach (var pair in _animators)
                if (pair.Key != null) pair.Key.speed = pair.Value;
            damageView?.SetDamageEnabled(_damageEnabled);
            Clear();
        }

        public void Clear()
        {
            _bodies.Clear();
            _animators.Clear();
            HasSnapshot = false;
        }
    }
}
