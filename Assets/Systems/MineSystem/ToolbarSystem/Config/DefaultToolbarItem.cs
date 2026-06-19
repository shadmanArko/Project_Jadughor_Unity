using System;
using Systems.MineSystem.InventorySystem.Model;
using UnityEngine;

namespace Systems.MineSystem.ToolbarSystem.Scriptable
{
    [Serializable]
    public sealed class DefaultToolbarItem
    {
        [SerializeField] private string id;
        [SerializeField] private string itemName;
        [SerializeField] private string type;
        [SerializeField] private string category;
        [SerializeField] private string variant;
        [Min(1)]
        [SerializeField] private int quantity = 1;

        public int Quantity => Mathf.Max(1, quantity);

        public Item CreateItem(int entryIndex, int instanceIndex)
        {
            var baseId = string.IsNullOrWhiteSpace(id)
                ? $"{type}.{category}.{variant}"
                : id.Trim();

            return new Item
            {
                Id = $"{baseId}.{entryIndex}.{instanceIndex}",
                Name = itemName,
                Type = type,
                Category = category,
                Variant = variant
            };
        }
    }
}
