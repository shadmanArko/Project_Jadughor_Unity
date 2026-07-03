using UnityEngine;

namespace Systems.MineSystem.DayAndTimeSystem.Configs
{
    [CreateAssetMenu(fileName = "DayAndTimeConfig", menuName = "Config/Day And Time Config")]
    public class DayAndTimeConfig : ScriptableObject
    {
        [Header("Real Time")]
        [Tooltip("IRL seconds per tick")]
        public float tickIntervalSeconds = 5f;

        [Header("In-Game Time")]
        [Tooltip("Game minutes that pass per tick")]
        public int minuteStep = 10;

        [Tooltip("Last minute value of an hour before rollover")]
        public int maxMinute = 50;

        [Header("Day Settings")]
        [Tooltip("Hour the game day starts at")]
        public int dayStartHour = 8;

        [Tooltip("Hour that triggers end of day (24 = midnight)")]
        public int dayEndHour = 24;

        [Tooltip("Total number of in-game days")]
        public int totalDays = 7;
    }
}