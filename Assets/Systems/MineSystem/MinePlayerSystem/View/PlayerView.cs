using UnityEngine;

namespace Systems.MineSystem.MinePlayerSystem.View
{
    public sealed class PlayerView : MonoBehaviour
    {
        [SerializeField] private Rigidbody2D body;
        [SerializeField] private Collider2D playerCollider;
        [SerializeField] private Animator animator;

        public Rigidbody2D Body => body;
        public Collider2D PlayerCollider => playerCollider;
        public Animator Animator => animator;

        public void MoveTo(Vector2 position)
        {
            body.MovePosition(position);
        }

        public void Stop()
        {
            body.linearVelocity = Vector2.zero;
        }
    }
}
