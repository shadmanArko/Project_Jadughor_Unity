using System;
using System.Collections.Generic;
using UniRx;
using UnityEngine;

namespace Core.EventBus
{
    /// <summary>
    /// Global, type-safe, reactive pub-sub EventBus.
    /// Bound as a singleton in ProjectContext via CoreInstaller.
    /// Use Receive<T>() to subscribe; use Publish<T>() to dispatch.
    /// Always call .AddTo(_disposables) or dispose subscriptions manually.
    /// </summary>
    public sealed class EventBus : IDisposable
    {
        private readonly Dictionary<Type, object> _subjects = new();
        private bool _isDisposed;

        // ─────────────────────────────────────────────────────────────────────
        //  Public API
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Publish an event to all current subscribers of type T.
        /// </summary>
        public void Publish<T>(T evt) where T : IEvent
        {
            if (_isDisposed)
            {
                Debug.LogWarning($"[EventBus] Tried to publish '{typeof(T).Name}' after disposal.");
                return;
            }

            if (_subjects.TryGetValue(typeof(T), out var rawSubject))
                ((Subject<T>)rawSubject).OnNext(evt);
        }

        /// <summary>
        /// Returns an IObservable you can subscribe to for events of type T.
        /// The stream is hot and shared — subscribing does not replay past events.
        /// </summary>
        public IObservable<T> Receive<T>() where T : IEvent
        {
            if (_isDisposed)
            {
                Debug.LogWarning($"[EventBus] Tried to receive '{typeof(T).Name}' after disposal.");
                return Observable.Empty<T>();
            }

            if (!_subjects.ContainsKey(typeof(T)))
                _subjects[typeof(T)] = new Subject<T>();

            return (Subject<T>)_subjects[typeof(T)];
        }

        // ─────────────────────────────────────────────────────────────────────
        //  IDisposable
        // ─────────────────────────────────────────────────────────────────────

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            foreach (var subjectObj in _subjects.Values)
            {
                if (subjectObj is IDisposable disposable)
                    disposable.Dispose();
            }

            _subjects.Clear();
        }
    }
}
