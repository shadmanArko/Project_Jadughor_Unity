using System;
using System.Collections.Generic;

namespace Systems.MineSystem.EnemySystem.Mob.BlackBat.Service
{
    public sealed class BatFormationService : IDisposable
    {
        private readonly Dictionary<Guid, int> _slotsByEnemy = new();
        private readonly HashSet<int> _assignedSlots = new();
        private bool _disposed;

        public int Register(Guid enemyId)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(BatFormationService));
            if (_slotsByEnemy.TryGetValue(enemyId, out var existingSlot))
                return existingSlot;

            var slot = 0;
            while (_assignedSlots.Contains(slot))
                slot++;
            _slotsByEnemy.Add(enemyId, slot);
            _assignedSlots.Add(slot);
            return slot;
        }

        public void Unregister(Guid enemyId)
        {
            if (!_slotsByEnemy.Remove(enemyId, out var slot))
                return;
            _assignedSlots.Remove(slot);
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _slotsByEnemy.Clear();
            _assignedSlots.Clear();
        }
    }
}
