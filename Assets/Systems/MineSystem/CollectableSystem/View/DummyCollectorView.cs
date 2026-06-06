using UnityEngine;

namespace Systems.MineSystem.CollectableSystem.View
{
    public sealed class DummyCollectorView : MonoBehaviour
    {
        [SerializeField] private Rigidbody2D body;
        [SerializeField] private Collider2D collectorCollider;
        [SerializeField] private Transform collectionPoint;

        public Rigidbody2D Body => body;
        public Collider2D CollectorCollider => collectorCollider;
        public Transform CollectionPoint =>
            collectionPoint != null ? collectionPoint : transform;
    }
}
