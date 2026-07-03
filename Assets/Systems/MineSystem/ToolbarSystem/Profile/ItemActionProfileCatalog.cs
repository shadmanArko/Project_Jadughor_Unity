using System;
using System.Collections.Generic;
using Systems.MineSystem.InventorySystem.Model;
using UnityEngine;

namespace Systems.MineSystem.ToolbarSystem.Profile
{
    [CreateAssetMenu(fileName = "ItemActionProfileCatalog", menuName = "Toolbar Actions/Profile Catalog")]
    public sealed class ItemActionProfileCatalog : ScriptableObject
    {
        [Min(0)]
        [SerializeField] private int equippableImpactMarker = 1;
        [SerializeField] private List<ItemActionProfile> profiles = new();

        private Dictionary<string, ItemActionProfile> _lookup;

        public int EquippableImpactMarker =>
            Mathf.Max(0, equippableImpactMarker);

        public bool TryGet(Item item, out ItemActionProfile profile)
        {
            EnsureLookup();
            if (item != null &&
                _lookup.TryGetValue(
                    BuildKey(item.Type, item.Category, item.Variant),
                    out profile))
                return true;

            profile = null;
            return false;
        }

        private void EnsureLookup()
        {
            if (_lookup != null)
                return;

            _lookup = new Dictionary<string, ItemActionProfile>(
                StringComparer.Ordinal);
            for (var index = 0; index < profiles.Count; index++)
            {
                var profile = profiles[index];
                if (profile == null)
                    continue;

                var key = BuildKey(
                    profile.ItemType,
                    profile.ItemCategory,
                    profile.ItemVariant);
                if (string.IsNullOrWhiteSpace(profile.ItemType) ||
                    string.IsNullOrWhiteSpace(profile.ItemCategory) ||
                    string.IsNullOrWhiteSpace(profile.ItemVariant) ||
                    !_lookup.TryAdd(key, profile))
                {
                    Debug.LogError(
                        $"Duplicate or invalid toolbar action profile '{profile.name}'.",
                        this);
                }
            }
        }

        private static string BuildKey(
            string type,
            string category,
            string variant)
        {
            return $"{Normalize(type)}|{Normalize(category)}|{Normalize(variant)}";
        }

        private static string Normalize(string value)
        {
            return value?.Trim().ToUpperInvariant() ?? string.Empty;
        }

        private void OnValidate()
        {
            _lookup = null;
        }

        private void OnEnable()
        {
            _lookup = null;
        }
    }
}
