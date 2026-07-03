using System;
using System.Collections.Generic;
using Systems.MineSystem.PauseSystem.Interface;
using UniRx;

namespace Systems.MineSystem.PauseSystem.Model
{
    public sealed class PauseModel : IDisposable
    {
        private readonly List<IPausable> _pausables = new();
        private readonly List<IPausable> _pausedMembers = new();
        private readonly List<IPauser> _pausers = new();
        private readonly ReactiveProperty<bool> _isPaused = new(false);
        private bool _disposed;

        public IReadOnlyList<IPausable> Pausables => _pausables;
        public IReadOnlyList<IPausable> PausedMembers => _pausedMembers;
        public IReadOnlyList<IPauser> Pausers => _pausers;
        public IReadOnlyReactiveProperty<bool> IsPaused => _isPaused;

        public bool Register(IPausable pausable) =>
            pausable != null && AddReference(_pausables, pausable);

        public bool Unregister(IPausable pausable)
        {
            RemoveReference(_pausedMembers, pausable);
            return RemoveReference(_pausables, pausable);
        }

        public bool AddPauser(IPauser pauser)
        {
            if (pauser == null || string.IsNullOrWhiteSpace(pauser.PauserId))
                return false;

            for (var i = 0; i < _pausers.Count; i++)
            {
                if (ReferenceEquals(_pausers[i], pauser))
                    return false;
                if (string.Equals(
                        _pausers[i].PauserId,
                        pauser.PauserId,
                        StringComparison.Ordinal))
                    return false;
            }

            _pausers.Add(pauser);
            return true;
        }

        public bool RemovePauser(IPauser pauser) =>
            RemoveReference(_pausers, pauser);

        public bool MarkPaused(IPausable pausable) =>
            AddReference(_pausedMembers, pausable);

        public bool MarkUnpaused(IPausable pausable) =>
            RemoveReference(_pausedMembers, pausable);

        public bool IsMemberPaused(IPausable pausable) =>
            ContainsReference(_pausedMembers, pausable);

        public void SetPaused(bool paused) => _isPaused.Value = paused;

        private static bool AddReference<T>(List<T> values, T value)
            where T : class
        {
            if (ContainsReference(values, value))
                return false;
            values.Add(value);
            return true;
        }

        private static bool RemoveReference<T>(List<T> values, T value)
            where T : class
        {
            for (var i = values.Count - 1; i >= 0; i--)
            {
                if (!ReferenceEquals(values[i], value))
                    continue;
                values.RemoveAt(i);
                return true;
            }
            return false;
        }

        private static bool ContainsReference<T>(List<T> values, T value)
            where T : class
        {
            for (var i = 0; i < values.Count; i++)
            {
                if (ReferenceEquals(values[i], value))
                    return true;
            }
            return false;
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _pausables.Clear();
            _pausedMembers.Clear();
            _pausers.Clear();
            _isPaused.Dispose();
        }
    }
}
