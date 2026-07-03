using Systems.MineSystem.ToolbarSystem.Enum;
using UnityEngine;

namespace Systems.MineSystem.ToolbarSystem.Profile
{
    public abstract class ItemActionProfile :
        ScriptableObject,
        IItemActionProfile
    {
        [Header("Exact Item Match")]
        [SerializeField] private string itemType;
        [SerializeField] private string itemCategory;
        [SerializeField] private string itemVariant;

        [Header("Presentation")]
        [SerializeField] private Sprite iconSprite;

        public string ItemType => itemType;
        public string ItemCategory => itemCategory;
        public string ItemVariant => itemVariant;
        public Sprite IconSprite => iconSprite;
        public abstract ItemActionKind ActionKind { get; }
    }
}
