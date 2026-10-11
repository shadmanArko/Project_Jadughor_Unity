using System;
using System.Collections.Generic;
using Systems.MineSystem.PauseSystem.Enum;
using Systems.MineSystem.PauseSystem.Interface;
using UniRx;

namespace Systems.MineSystem.PauseSystem.Model
{
    /// <summary>
    /// Registered pausables and the pausers currently holding a pause, each
    /// tagged with an area so a request can freeze one area or all of them.
    /// </summary>
    /// <remarks>
    /// Areas are held in lists kept index-parallel with their owners
    /// (<c>_pausables</c>/<c>_pausableAreas</c> and
    /// <c>_pausers</c>/<c>_pauserTargets</c>), matching the reference-equality
    /// scanning the rest of this class uses instead of dictionaries - the lists
    /// are small and only walked on pause transitions. Every mutation of a pair
    /// must go through the index from <see cref="IndexOfReference{T}"/> so the
    /// two lists never drift apart.
    /// </remarks>
    public sealed class PauseModel : IDisposable
    {
        private readonly List<IPausable> _pausables = new();
        private readonly List<PauseArea> _pausableAreas = new();
        private readonly List<IPausable> _pausedMembers = new();
        private readonly List<IPauser> _pausers = new();
        private readonly List<PauseArea?> _pauserTargets = new();
        private readonly ReactiveProperty<bool> _isPaused = new(false);
        private bool _disposed;

        public IReadOnlyList<IPausable> Pausables => _pausables;
        public IReadOnlyList<IPausable> PausedMembers => _pausedMembers;
        public IReadOnlyList<IPauser> Pausers => _pausers;

        /// <summary>
        /// True while at least one pauser is active, regardless of the area it
        /// targets. This does not imply any particular member is frozen - ask
        /// <see cref="ShouldBePaused"/> for that.
        /// </summary>
        public IReadOnlyReactiveProperty<bool> IsPaused => _isPaused;

        /// <summary>
        /// Registers a pausable, or updates the area of one already registered
        /// (pooled objects re-register with a new area when they are reused).
        /// Returns true only when it was newly added.
        /// </summary>
        public bool Register(IPausable pausable, PauseArea area)
        {
            if (pausable == null)
                return false;

            var index = IndexOfReference(_pausables, pausable);
            if (index >= 0)
            {
                _pausableAreas[index] = area;
                return false;
            }

            _pausables.Add(pausable);
            _pausableAreas.Add(area);
            return true;
        }

        public bool Unregister(IPausable pausable)
        {
            RemoveReference(_pausedMembers, pausable);

            var index = IndexOfReference(_pausables, pausable);
            if (index < 0)
                return false;

            _pausables.RemoveAt(index);
            _pausableAreas.RemoveAt(index);
            return true;
        }

        public bool TryGetArea(IPausable pausable, out PauseArea area)
        {
            var index = IndexOfReference(_pausables, pausable);
            if (index < 0)
            {
                area = default;
                return false;
            }

            area = _pausableAreas[index];
            return true;
        }

        public bool AddPauser(IPauser pauser, PauseArea? targetArea)
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
            _pauserTargets.Add(targetArea);
            return true;
        }

        public bool RemovePauser(IPauser pauser)
        {
            var index = IndexOfReference(_pausers, pauser);
            if (index < 0)
                return false;

            _pausers.RemoveAt(index);
            _pauserTargets.RemoveAt(index);
            return true;
        }

        /// <summary>
        /// True when any active pauser targets this area, either explicitly or
        /// by targeting every area.
        /// </summary>
        public bool ShouldBePaused(PauseArea area)
        {
            for (var i = 0; i < _pauserTargets.Count; i++)
            {
                var target = _pauserTargets[i];
                if (!target.HasValue || target.Value == area)
                    return true;
            }
            return false;
        }

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
            var index = IndexOfReference(values, value);
            if (index < 0)
                return false;
            values.RemoveAt(index);
            return true;
        }

        private static bool ContainsReference<T>(List<T> values, T value)
            where T : class => IndexOfReference(values, value) >= 0;

        private static int IndexOfReference<T>(List<T> values, T value)
            where T : class
        {
            for (var i = 0; i < values.Count; i++)
            {
                if (ReferenceEquals(values[i], value))
                    return i;
            }
            return -1;
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _pausables.Clear();
            _pausableAreas.Clear();
            _pausedMembers.Clear();
            _pausers.Clear();
            _pauserTargets.Clear();
            _isPaused.Dispose();
        }
    }
}
