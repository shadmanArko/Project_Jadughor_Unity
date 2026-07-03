using Systems.MineSystem.CollectableSystem.View;
using UnityEngine;

namespace Systems.MineSystem.CollectableSystem.Scriptable
{
    [CreateAssetMenu(
        fileName = "CollectableSystemConfig",
        menuName = "Config/Collectable System Config")]
    public sealed class CollectableSystemConfig : ScriptableObject
    {
        [Header("Movement")]
        [Min(0f)] public float pullSpeed = 4f;
        [Min(0.02f)] public float collectorScanInterval = 0.15f;
        [Min(0.01f)] public float droppedItemGravityScale = 1f;
        [Min(0f)] public float attractionDelay = 0.35f;

        [Header("Prefab")]
        public CollectableView commonCollectablePrefab;

        [Header("Pool")]
        [Min(0)] public int initialPoolSize = 20;
        [Tooltip("Zero means the pool can grow without a configured limit.")]
        [Min(0)] public int maximumPoolSize;
    }
}
