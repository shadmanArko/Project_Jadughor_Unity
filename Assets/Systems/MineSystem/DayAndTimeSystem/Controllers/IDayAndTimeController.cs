using Systems.MineSystem.DayAndTimeSystem.Structs;
using UniRx;
using UnityEngine;

namespace Systems.MineSystem.DayAndTimeSystem.Controllers
{
    public interface IDayAndTimeController
    {
        void  Pause();
        void  Resume();
        void  SetTime(MineTime time);
        MineTime  GetCurrentTime();

        // Read-only access to reactive state for scripts that want to observe
        IReadOnlyReactiveProperty<int> Day    { get; }
        IReadOnlyReactiveProperty<int> Hour   { get; }
        IReadOnlyReactiveProperty<int> Minute { get; }
    }
}