using System.Collections.Generic;
using Systems.MineSystem.CollectableSystem.Interface;
using UnityEngine;

namespace Systems.MineSystem.CollectableSystem.Service
{
    public sealed class CollectorRegistry
    {
        private readonly List<ICollector> _collectors = new();
        private readonly Dictionary<int, ICollector> _collectorByCollider = new();

        public IReadOnlyList<ICollector> Collectors => _collectors;

        public void Register(ICollector collector)
        {
            if (collector == null || _collectors.Contains(collector))
                return;

            _collectors.Add(collector);
            if (collector.CollectorCollider != null)
                _collectorByCollider[collector.CollectorCollider.GetInstanceID()] = collector;
        }

        public void Unregister(ICollector collector)
        {
            if (collector == null)
                return;

            _collectors.Remove(collector);
            if (collector.CollectorCollider != null)
                _collectorByCollider.Remove(collector.CollectorCollider.GetInstanceID());
        }

        public bool TryGetCollector(Collider2D collider, out ICollector collector)
        {
            collector = null;
            return collider != null &&
                   _collectorByCollider.TryGetValue(collider.GetInstanceID(), out collector);
        }
    }
}
