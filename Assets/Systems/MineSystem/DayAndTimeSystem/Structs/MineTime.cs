using System;

namespace Systems.MineSystem.DayAndTimeSystem.Structs
{
    [Serializable]
    public struct MineTime
    {
        public int day;
        public int hour;
        public int minute;

        public MineTime(int day, int hour, int minute)
        {
            this.day    = day;
            this.hour   = hour;
            this.minute = minute;
        }

        public override string ToString() => $"Day {day:D2} | {hour:D2}:{minute:D2}";
    }
}