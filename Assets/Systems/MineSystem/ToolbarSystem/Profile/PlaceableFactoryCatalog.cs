using System;
using System.Collections.Generic;
using Systems.MineSystem.ToolbarSystem.Model;
using UnityEngine;

namespace Systems.MineSystem.ToolbarSystem.Profile
{
    [CreateAssetMenu(fileName = "PlaceableFactoryCatalog", menuName = "Toolbar Actions/Placeable Factory Catalog")]
    public sealed class PlaceableFactoryCatalog : ScriptableObject
    {
        [SerializeField] private List<PlaceableFactoryEntry> entries = new();
        private Dictionary<string, PlaceableFactoryEntry> _lookup;
        public IReadOnlyList<PlaceableFactoryEntry> Entries => entries;

        public bool TryGet(string id, out PlaceableFactoryEntry entry)
        {
            EnsureLookup();
            if (!string.IsNullOrWhiteSpace(id) &&
                _lookup.TryGetValue(id.Trim(), out entry))
                return true;

            entry = null;
            return false;
        }

        private void EnsureLookup()
        {
            if (_lookup != null)
                return;

            _lookup = new Dictionary<string, PlaceableFactoryEntry>(
                StringComparer.OrdinalIgnoreCase);
            foreach (var entry in entries)
            {
                if (entry == null ||
                    string.IsNullOrWhiteSpace(entry.id) ||
                    entry.prefab == null ||
                    !_lookup.TryAdd(entry.id.Trim(), entry))
                {
                    Debug.LogError(
                        "Placeable factory catalog contains an invalid or duplicate entry.",
                        this);
                }
            }
        }

        private void OnValidate()
        {
            _lookup = null;
        }
    }
}
