using Systems.MineSystem.ToolbarSystem.Enum;
using UnityEngine;

namespace Systems.MineSystem.ToolbarSystem.Profile
{
    [CreateAssetMenu(fileName = "ConsumableActionProfile", menuName = "Toolbar Actions/Consumable Profile")]
    public sealed class ConsumableActionProfile : ItemActionProfile
    {
        [Header("Effect")]
        [SerializeField] private ConsumableStat stat;
        [Min(0f)]
        [SerializeField] private float restoreAmount = 10f;

        [Header("Animation")]
        [SerializeField] private string animationId;
        [Min(0)]
        [SerializeField] private int consumeMarker = 1;

        public override ItemActionKind ActionKind => ItemActionKind.Consumable;
        public ConsumableStat Stat => stat;
        public float RestoreAmount => Mathf.Max(0f, restoreAmount);
        public string AnimationId => animationId;
        public int ConsumeMarker => Mathf.Max(0, consumeMarker);
    }
}
