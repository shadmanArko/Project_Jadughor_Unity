using Systems.MineSystem.ToolbarSystem.Enum;
using UnityEngine;

namespace Systems.MineSystem.ToolbarSystem.Profile
{
    [CreateAssetMenu(fileName = "WeaponActionProfile", menuName = "Toolbar Actions/Weapon Profile")]
    public sealed class WeaponActionProfile : EquippableActionProfile
    {
        [Min(0f)]
        [SerializeField] private float damage = 10f;
        [Min(0.01f)]
        [SerializeField] private float hitRadius = 0.5f;
        [SerializeField] private LayerMask targetLayers = ~0;

        public override ItemActionKind ActionKind => ItemActionKind.Weapon;
        public float Damage => Mathf.Max(0f, damage);
        public float HitRadius => Mathf.Max(0.01f, hitRadius);
        public LayerMask TargetLayers => targetLayers;
    }
}
