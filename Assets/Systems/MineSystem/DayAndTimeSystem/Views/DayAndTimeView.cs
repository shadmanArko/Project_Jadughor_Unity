using Systems.MineSystem.DayAndTimeSystem.Models;
using TMPro;
using UniRx;
using UnityEngine;

namespace Systems.MineSystem.DayAndTimeSystem.Views
{
    public class DayAndTimeView : MonoBehaviour
    {
        [SerializeField] private TMP_Text dateTimeText;

        public void Bind(DayAndTimeModel model)
        {
            model.Day
                .CombineLatest(model.Hour, model.Minute,
                    FormatDisplay)
                .Subscribe(text => dateTimeText.text = text)
                .AddTo(this);
        }

        private static string FormatDisplay(int day, int hour, int minute)
            => $"Day(s) {day:D2}, {hour:D2}:{minute:D2}";
    }
}