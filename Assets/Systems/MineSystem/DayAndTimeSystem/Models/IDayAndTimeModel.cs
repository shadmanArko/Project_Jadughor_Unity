using Systems.MineSystem.DayAndTimeSystem.Structs;
using UniRx;

namespace Systems.MineSystem.DayAndTimeSystem.Models
{
    public interface IDayAndTimeModel
    {
        IReadOnlyReactiveProperty<int> Day { get; }
        IReadOnlyReactiveProperty<int> Hour { get; }
        IReadOnlyReactiveProperty<int> Minute { get; }
        void StartTime();
        void Pause();
        void Resume();
        void SetTime(MineTime time);
        MineTime GetCurrentTime();
        void Dispose();
    }
}