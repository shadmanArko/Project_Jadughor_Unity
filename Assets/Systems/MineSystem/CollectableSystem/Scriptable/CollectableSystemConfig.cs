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

        [Header("Prefabs")]
        public CollectableView resourceCollectablePrefab;
        public CollectableView artifactCollectablePrefab;
        public CollectableView cellPlaceableCollectablePrefab;
        public CollectableView wallPlaceableCollectablePrefab;

        [Header("Resource Pool")]
        [Min(0)] public int resourceInitialSize = 12;
        [Min(1)] public int resourceMaxSize = 100;

        [Header("Artifact Pool")]
        [Min(0)] public int artifactInitialSize = 6;
        [Min(1)] public int artifactMaxSize = 50;

        [Header("Cell Placeable Pool")]
        [Min(0)] public int cellPlaceableInitialSize = 4;
        [Min(1)] public int cellPlaceableMaxSize = 30;

        [Header("Wall Placeable Pool")]
        [Min(0)] public int wallPlaceableInitialSize = 4;
        [Min(0)] public int wallPlaceableMaxSize = 30;
    }
}
