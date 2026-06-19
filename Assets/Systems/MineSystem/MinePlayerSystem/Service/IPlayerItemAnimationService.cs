using System;
using Systems.MineSystem.MinePlayerSystem.SubSystem.PlayerAnimationSubSystem.Model;

namespace Systems.MineSystem.MinePlayerSystem.Service
{
    public interface IPlayerItemAnimationService
    {
        IObservable<PlayerAnimationMarkerEvent> MarkerReached { get; }
        IObservable<PlayerAnimationCompletedEvent> ActionCompleted { get; }
        IObservable<string> ActionFailed { get; }
        string ActiveAnimationId { get; }
        bool TryRequestItemAction(string animationId);
    }
}
