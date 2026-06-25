using UnityEngine;

namespace Systems.MineSystem.Mine.Service.VisualizerService
{
    [CreateAssetMenu(
        fileName = "CaveFormationConfig",
        menuName = "Scriptable/Cave Formation Config")]
    public sealed class CaveFormationConfig : ScriptableObject
    {
        [Header("Prefabs")]
        public GameObject stalactitePrefab;
        public GameObject stalagmitePrefab;

        [Header("Pooling")]
        [Min(0)] public int stalactiteInitialPoolSize = 8;
        [Min(0)] public int stalactiteMaxPoolSize = 64;
        [Min(0)] public int stalagmiteInitialPoolSize = 8;
        [Min(0)] public int stalagmiteMaxPoolSize = 64;

        [Header("Health")]
        [Min(1f)] public float minHealth = 5f;
        [Min(1f)] public float maxHealth = 10f;

        [Header("Stalactite")]
        [Min(0.1f)] public float stalactiteFallSpeed = 8f;
        [Min(0f)] public float stalactiteDamage = 15f;
        [Min(0.05f)] public float stalactiteImpactRadius = 0.45f;
        [Min(1f)] public float stalactiteMaxFallDistance = 40f;
        public LayerMask stalactiteTargetLayers = ~0;

        [Header("Stalagmite")]
        [Min(0f)] public float stalagmiteContactDamage = 10f;

        [Header("Animation States")]
        public string intactState = "Intact";
        public string collapseState = "Collapse";
        public string fallState = "Fall";
        public string shatterState = "Shatter";

        [Header("Animation Durations")]
        [Min(0f)] public float collapseDuration = 0.95f;
        [Min(0f)] public float shatterDuration = 0.85f;

        public float RandomHealth =>
            Random.Range(
                Mathf.Min(minHealth, maxHealth),
                Mathf.Max(minHealth, maxHealth));
    }
}
