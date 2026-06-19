using System;
using System.Collections.Generic;
using Systems.MineSystem.InventorySystem.Model;
using Systems.MineSystem.MinePlayerSystem.Service;
using Systems.MineSystem.ToolbarSystem.Interface;
using Systems.MineSystem.ToolbarSystem.Enum;
using Systems.MineSystem.ToolbarSystem.Model;
using Systems.MineSystem.ToolbarSystem.Profile;
using UniRx;
using UnityEngine;

namespace Systems.MineSystem.ToolbarSystem.Handler
{
    public abstract class AnimatedItemActionHandler<TProfile> :
        IItemActionHandler,
        IDisposable
        where TProfile : ItemActionProfile
    {
        private readonly IPlayerItemAnimationService _animation;
        private readonly IToolbarNavigationLock _navigationLock;
        private readonly CompositeDisposable _disposables = new();

        protected Item ActiveItem { get; private set; }
        protected int ActiveSlot { get; private set; }
        protected TProfile ActiveProfile { get; private set; }

        private Item _pendingItem;
        private int _pendingSlot;
        private TProfile _pendingProfile;
        private ItemActionTarget _pendingTarget;
        private string _pendingAnimationId;
        private int _pendingMarker;
        private bool _impactApplied;
        private readonly HashSet<string> _invalidActions = new();

        public abstract ItemActionKind ActionKind { get; }

        protected AnimatedItemActionHandler(
            IPlayerItemAnimationService animation,
            IToolbarNavigationLock navigationLock)
        {
            _animation = animation;
            _navigationLock = navigationLock;

            _animation.MarkerReached
                .Subscribe(OnMarkerReached)
                .AddTo(_disposables);
            _animation.ActionCompleted
                .Subscribe(OnActionCompleted)
                .AddTo(_disposables);
            _animation.ActionFailed
                .Subscribe(OnActionFailed)
                .AddTo(_disposables);
        }

        public void Activate(
            Item item,
            int slotIndex,
            ItemActionProfile profile)
        {
            ActiveItem = item;
            ActiveSlot = slotIndex;
            ActiveProfile = profile as TProfile;
        }

        public virtual void Deactivate()
        {
            ActiveItem = null;
            ActiveProfile = null;
            ActiveSlot = -1;
        }

        public bool TryExecute()
        {
            if (ActiveItem == null ||
                ActiveProfile == null ||
                _pendingItem != null ||
                !TryPrepareAction(
                    ActiveProfile,
                    out var animationId,
                    out var marker,
                    out var target))
            {
                WarnInvalidAction();
                return false;
            }

            if (!_animation.TryRequestItemAction(animationId))
                return false;

            _pendingItem = ActiveItem;
            _pendingSlot = ActiveSlot;
            _pendingProfile = ActiveProfile;
            _pendingAnimationId = animationId;
            _pendingMarker = marker;
            _pendingTarget = target;
            _impactApplied = false;
            _navigationLock.SetNavigationLocked(true);
            return true;
        }

        private void WarnInvalidAction()
        {
            if (ActiveItem == null)
                return;

            var key =
                $"{ActiveItem.Type}|{ActiveItem.Category}|{ActiveItem.Variant}";
            if (_invalidActions.Add(key))
            {
                Debug.LogWarning(
                    $"Toolbar action '{key}' has no valid animation for the current target direction/state.");
            }
        }

        protected abstract bool TryPrepareAction(
            TProfile profile,
            out string animationId,
            out int marker,
            out ItemActionTarget target);

        protected abstract void ApplyImpact(
            Item item,
            int slotIndex,
            TProfile profile,
            ItemActionTarget target);

        private void OnMarkerReached(
            MinePlayerSystem.SubSystem.PlayerAnimationSubSystem.Model.PlayerAnimationMarkerEvent animationEvent)
        {
            if (_pendingItem == null ||
                _impactApplied ||
                animationEvent.AnimationId != _pendingAnimationId ||
                animationEvent.Marker != _pendingMarker)
                return;

            _impactApplied = true;
            ApplyImpact(
                _pendingItem,
                _pendingSlot,
                _pendingProfile,
                _pendingTarget);
        }

        private void OnActionCompleted(
            MinePlayerSystem.SubSystem.PlayerAnimationSubSystem.Model.PlayerAnimationCompletedEvent animationEvent)
        {
            if (_pendingItem == null ||
                animationEvent.AnimationId != _pendingAnimationId)
                return;

            ClearPendingAction();
        }

        private void OnActionFailed(string animationId)
        {
            if (_pendingItem == null ||
                animationId != _pendingAnimationId)
                return;

            ClearPendingAction();
        }

        private void ClearPendingAction()
        {
            _pendingItem = null;
            _pendingProfile = null;
            _pendingAnimationId = null;
            _pendingSlot = -1;
            _impactApplied = false;
            _navigationLock.SetNavigationLocked(false);
        }

        public void Dispose()
        {
            ClearPendingAction();
            _disposables.Dispose();
        }
    }
}
