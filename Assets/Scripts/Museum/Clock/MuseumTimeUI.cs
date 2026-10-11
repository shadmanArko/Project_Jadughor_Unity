using ProjectMuseum.Narrative;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectMuseum.Clock
{
    /// <summary>
    /// The date/time panel — port of Godot's <c>DateTimeUi</c>. Pure view: it only shows what
    /// <see cref="MuseumTimeSystem"/> broadcasts and turns button presses into events.
    /// All objects are authored in the Time Canvas prefab (Tools ▸ Project Museum ▸ HUD ▸
    /// Build Time UI); the buttons' OnClick call the public methods below.
    ///
    /// Shows: clock "HH:MM", day name, day "DD", season icon, year "Yr NN".
    /// Unity addition: the active speed button and the pause button (while paused) are tinted,
    /// since Godot never showed which speed was running.
    /// </summary>
    public class MuseumTimeUI : MonoBehaviour
    {
        [Header("Labels")]
        [SerializeField] private TMP_Text clockText;
        [SerializeField] private TMP_Text dayNameText;
        [SerializeField] private TMP_Text dayText;
        [SerializeField] private TMP_Text yearText;

        [Header("Season icon (month 1-4)")]
        [SerializeField] private Image seasonIcon;
        [Tooltip("Spring, Summer, Autumn, Winter.")]
        [SerializeField] private Sprite[] seasonSprites = new Sprite[4];

        [Header("Day names (day 1 = first entry)")]
        [SerializeField] private string[] dayNames = { "Mo", "Tu", "We", "Th", "Fr", "Sa", "Su" };

        [Header("Buttons")]
        [SerializeField] private Image pauseButtonImage;
        [Tooltip("Speed button backgrounds, same order as Speed Values.")]
        [SerializeField] private Image[] speedButtonImages;
        [SerializeField] private int[] speedValues = { 1, 2, 4 };
        [SerializeField] private Color idleColor = Color.white;
        [Tooltip("Active speed / paused tint (Godot theme focus colour #FDD053).")]
        [SerializeField] private Color activeColor = new Color32(0xFD, 0xD0, 0x53, 0xFF);

        private void OnEnable()
        {
            MuseumActions.OnTimeUpdated += ShowTime;
            MuseumActions.OnTimePauseValueUpdated += ShowPaused;
            MuseumActions.OnTimeSpeedChanged += ShowSpeed;
        }

        private void OnDisable()
        {
            MuseumActions.OnTimeUpdated -= ShowTime;
            MuseumActions.OnTimePauseValueUpdated -= ShowPaused;
            MuseumActions.OnTimeSpeedChanged -= ShowSpeed;
        }

        // ── Button hooks (wired in the prefab) ──────────────────────────

        public void OnPausePlayPressed() => MuseumActions.OnClickPausePlayButton?.Invoke();

        public void OnSpeedPressed(int speed) => MuseumActions.OnClickTimeSpeedButton?.Invoke(speed);

        // ── Display ─────────────────────────────────────────────────────

        private void ShowTime(int minutes, int hours, int days, int months, int years)
        {
            if (clockText != null) clockText.text = $"{hours:D2}:{minutes:D2}";
            if (dayText != null) dayText.text = $"{days:D2}";
            if (yearText != null) yearText.text = $"Yr {years:D2}";
            if (dayNameText != null && dayNames.Length > 0)
                dayNameText.text = dayNames[(Mathf.Max(1, days) - 1) % dayNames.Length];

            if (seasonIcon != null && seasonSprites.Length > 0)
            {
                Sprite season = seasonSprites[Mathf.Clamp(months - 1, 0, seasonSprites.Length - 1)];
                if (season != null) seasonIcon.sprite = season;
            }
        }

        private void ShowPaused(bool paused)
        {
            if (pauseButtonImage != null) pauseButtonImage.color = paused ? activeColor : idleColor;
        }

        private void ShowSpeed(int speed)
        {
            for (int i = 0; i < speedButtonImages.Length; i++)
            {
                if (speedButtonImages[i] == null) continue;
                bool active = i < speedValues.Length && speedValues[i] == speed;
                speedButtonImages[i].color = active ? activeColor : idleColor;
            }
        }
    }
}
