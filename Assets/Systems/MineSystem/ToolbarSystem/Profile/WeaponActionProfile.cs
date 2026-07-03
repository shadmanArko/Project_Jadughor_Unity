using Systems.MineSystem.ToolbarSystem.Enum;
using UnityEngine;
using UnityEngine.Serialization;

namespace Systems.MineSystem.ToolbarSystem.Profile
{
    [CreateAssetMenu(fileName = "WeaponActionProfile", menuName = "Toolbar Actions/Weapon Profile")]
    public sealed class WeaponActionProfile : EquippableActionProfile
    {
        [Min(0f)]
        [SerializeField] private float damage = 10f;
        [Tooltip("Radius of the damage circle measured in mine cells.")]
        [FormerlySerializedAs("hitRadius")]
        [Min(0.01f)]
        [SerializeField] private float hitRadiusCells = 0.75f;
        [SerializeField] private LayerMask targetLayers = ~0;

        public override ItemActionKind ActionKind => ItemActionKind.Weapon;
        public float Damage => Mathf.Max(0f, damage);
        public float HitRadiusCells =>
            Mathf.Max(0.01f, hitRadiusCells);
        public LayerMask TargetLayers => targetLayers;
    }
}
