using System;
using System.Collections.Generic;
using System.Linq;
using Core.EventBus;
using InputSystem.Config;
using InputSystem.Data;
using InputSystem.Events;
using InputSystem.Model;
using UniRx;
using UnityEngine;
using Zenject;

namespace InputSystem.Service
{
    /// <summary>
    /// Manages a stack of ActionMapContextIds and translates them into
    /// enabled/disabled action maps on the InputActionAsset.
    ///
    /// Stack resolution rules:
    ///   The stack is replayed from bottom to top.
    ///   - IsAdditive = false → clear all accumulated maps, then add this context's maps.
    ///   - IsAdditive = true  → add this context's maps on top of whatever is active.
    ///
    /// API for other systems:
    ///   SwitchToContext()  — clear the stack, push one exclusive context.
    ///   PushContext()      — layer a context on top (e.g. Dialogue over Museum).
    ///   PopContext()       — return to the previous context.
    ///   ClearAllContexts() — disable all action maps (e.g. during cutscenes).
    /// </summary>
    public sealed class ActionMapService : IInitializable, IDisposable
    {
        // ─── Dependencies ──────────────────────────────────────────────────────

        private readonly IInputSystemModel      _inputModel;
        private readonly ActionMapContextConfig _config;
        private readonly EventBus               _eventBus;

        // ─── State ─────────────────────────────────────────────────────────────

        private readonly Stack<ActionMapContextId>               _contextStack = new();
        private readonly ReactiveProperty<ActionMapContextId>    _topContext   = new(ActionMapContextId.None);
        private readonly CompositeDisposable                     _disposables  = new();

        /// <summary>The context currently on the top of the stack (reactive).</summary>
        public IReadOnlyReactiveProperty<ActionMapContextId> TopContext => _topContext;

        // ─── Constructor ──────────────────────────────────────────────────────

        public ActionMapService(IInputSystemModel inputModel,
                                ActionMapContextConfig config,
                                EventBus eventBus)
        {
            _inputModel = inputModel;
            _config     = config;
            _eventBus   = eventBus;
        }

        // ─── IInitializable ───────────────────────────────────────────────────

        public void Initialize()
        {
            // All maps are already disabled by InputSystemModel constructor.
            // No further setup needed here — ActionMapService is ready to accept pushes.
            Debug.Log("[ActionMapService] Initialized. No context active.");
        }

        // ─── Public API ───────────────────────────────────────────────────────

        /// <summary>
        /// Clears the entire stack and makes this context the sole active one.
        /// Use for major scene/state transitions (e.g. Museum → Mine).
        /// </summary>
        public void SwitchToContext(ActionMapContextId contextId)
        {
            _contextStack.Clear();
            _contextStack.Push(contextId);
            ApplyStack();
        }

        /// <summary>
        /// Pushes a new context on top without removing the current one.
        /// Whether the new context's maps are additive or exclusive is defined
        /// in ActionMapContextConfig.
        /// </summary>
        public void PushContext(ActionMapContextId contextId)
        {
            _contextStack.Push(contextId);
            ApplyStack();
        }

        /// <summary>
        /// Removes the top context and restores the previous one.
        /// Safe to call when the stack is empty (no-op with a warning).
        /// </summary>
        public void PopContext()
        {
            if (_contextStack.Count == 0)
            {
                Debug.LogWarning("[ActionMapService] PopContext called on an empty stack.");
                return;
            }

            _contextStack.Pop();
            ApplyStack();
        }

        /// <summary>
        /// Empties the stack and disables all action maps.
        /// Useful during cutscenes or loading screens.
        /// </summary>
        public void ClearAllContexts()
        {
            _contextStack.Clear();
            _inputModel.DisableAllActionMaps();
            _topContext.Value = ActionMapContextId.None;

            _eventBus.Publish(new ActionMapContextChangedEvent(
                ActionMapContextId.None,
                Array.Empty<string>()));

            Debug.Log("[ActionMapService] All contexts cleared.");
        }

        /// <summary>Peek at the current top context without modifying the stack.</summary>
        public ActionMapContextId Peek()
            => _contextStack.Count > 0 ? _contextStack.Peek() : ActionMapContextId.None;

        /// <summary>Number of contexts currently on the stack.</summary>
        public int StackDepth => _contextStack.Count;

        // ─── Private Stack Resolution ─────────────────────────────────────────

        private void ApplyStack()
        {
            var activeMaps = ComputeActiveMaps();

            _inputModel.DisableAllActionMaps();
            foreach (var mapName in activeMaps)
                _inputModel.EnableActionMap(mapName);

            var top = _contextStack.Count > 0 ? _contextStack.Peek() : ActionMapContextId.None;
            _topContext.Value = top;

            _eventBus.Publish(new ActionMapContextChangedEvent(top, activeMaps.ToArray()));

            Debug.Log(
                $"[ActionMapService] Context stack applied. Top='{top}', " +
                $"ActiveMaps=[{string.Join(", ", activeMaps)}]");
        }

        /// <summary>
        /// Replays the stack from bottom to top, accumulating active map names.
        /// Non-additive contexts clear whatever was accumulated before them.
        /// </summary>
        private HashSet<string> ComputeActiveMaps()
        {
            var activeMaps  = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var stackArray  = _contextStack.ToArray();
            Array.Reverse(stackArray); // ToArray() gives top-first; we need bottom-first

            foreach (var contextId in stackArray)
            {
                var definition = _config.GetContext(contextId);

                if (definition == null)
                {
                    Debug.LogWarning(
                        $"[ActionMapService] No ActionMapContextDefinition found for '{contextId}'. " +
                        "Add it to ActionMapContextConfig.asset.");
                    continue;
                }

                if (!definition.IsAdditive)
                    activeMaps.Clear();

                foreach (var mapName in definition.EnabledActionMapNames)
                {
                    if (!string.IsNullOrWhiteSpace(mapName))
                        activeMaps.Add(mapName);
                }
            }

            return activeMaps;
        }

        // ─── IDisposable ──────────────────────────────────────────────────────

        public void Dispose()
        {
            _topContext.Dispose();
            _disposables.Dispose();
        }
    }
}
