using System;
using System.Collections.Generic;
using UniRx;

namespace Systems.Utilities.EventBus
{
    public class EventBus : IDisposable
    {
        private readonly Dictionary<Type, object> _subjects = new();
        private readonly CompositeDisposable _busDisposable = new();
        private bool _disposed;

        // ─── Fire ────────────────────────────────────────────────────────────

        /// <summary>Publishes a signal to all active subscribers.</summary>
        public void Fire<TSignal>(TSignal signal) where TSignal : struct
        {
            if (_disposed) return;
            GetOrCreate<TSignal>().OnNext(signal);
        }

        /// <summary>Publishes a signal with no data (empty struct shorthand).</summary>
        public void Fire<TSignal>() where TSignal : struct
        {
            Fire(default(TSignal));
        }

        // ─── Subscribe ───────────────────────────────────────────────────────

        /// <summary>
        /// Returns an IObservable for a signal type.
        /// Chain .Subscribe(...).AddTo(disposable) on this.
        /// </summary>
        public IObservable<TSignal> OnSignal<TSignal>() where TSignal : struct
        {
            return GetOrCreate<TSignal>().AsObservable();
        }

        /// <summary>
        /// Convenience: subscribe with an action and immediately bind to a disposable.
        /// Returns the IDisposable so you can chain further if needed.
        /// </summary>
        public IDisposable Subscribe<TSignal>(
            Action<TSignal> onSignal,
            ICollection<IDisposable> disposable) where TSignal : struct
        {
            var sub = OnSignal<TSignal>().Subscribe(onSignal);
            disposable.Add(sub);
            return sub;
        }

        // ─── Internal ────────────────────────────────────────────────────────

        private Subject<TSignal> GetOrCreate<TSignal>() where TSignal : struct
        {
            var type = typeof(TSignal);

            if (!_subjects.TryGetValue(type, out var existing))
            {
                var subject = new Subject<TSignal>();
                _subjects[type] = subject;
                _busDisposable.Add(subject); // bus owns subject lifetime
                return subject;
            }

            return (Subject<TSignal>)existing;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _busDisposable.Dispose();
            _subjects.Clear();
        }
    }
}