using ProjectMuseum.Builder;
using ProjectMuseum.Data;
using ProjectMuseum.Narrative;
using UnityEngine;
using UnityEngine.InputSystem;
using Zenject;

namespace ProjectMuseum.Clock
{
    /// <summary>
    /// The museum clock — port of Godot's <c>MuseumTimeSystem</c>.
    ///
    /// Rules (same as Godot):
    /// - Time moves in 10-minute ticks; one tick every <see cref="realSecondsPerTenMinutes"/>
    ///   real seconds at 1x (5 s → a game hour is 30 s, a day 12 min).
    /// - Speed multiplies the clock rate only (1x / 2x / 4x). Pausing stops the clock.
    /// - Calendar: 60 min, 24 h, 30 days a month, 4 months (seasons) a year.
    /// - Every tick raises <see cref="MuseumActions.OnTimeUpdated"/>.
    ///
    /// Unity additions: pause also freezes gameplay through <c>Time.timeScale</c> (guests,
    /// player — UI, camera and narrative run on unscaled time and keep working), the clock
    /// is stored in the museum save (<see cref="MuseumDataModel.Clock"/>), and there are
    /// keyboard shortcuts.
    /// </summary>
    public class MuseumTimeSystem : MonoBehaviour
    {
        private const int MinutesPerTick = 10;
        private const int HoursPerDay = 24;
        private const int DaysPerMonth = 30;
        private const int MonthsPerYear = 4;

        [Header("Clock")]
        [Tooltip("Real seconds per 10 game minutes at 1x (Godot: 5).")]
        [SerializeField] private float realSecondsPerTenMinutes = 5f;
        [Tooltip("Multipliers the speed buttons may ask for. Anything else is ignored.")]
        [SerializeField] private int[] allowedSpeeds = { 1, 2, 4 };

        [Header("Pause")]
        [Tooltip("Pausing also sets Time.timeScale = 0, so guests and the player freeze too.")]
        [SerializeField] private bool pauseFreezesGameplay = true;
        [Tooltip("Speed also scales Time.timeScale (guests walk faster). Godot: off.")]
        [SerializeField] private bool speedScalesGameplay;
        [Tooltip("Pressing a speed button while paused resumes the clock. Godot: off.")]
        [SerializeField] private bool speedButtonResumes;

        [Header("Shortcuts (None = off)")]
        [SerializeField] private Key pauseKey = Key.Space;
        [Tooltip("One key per entry in Allowed Speeds, in the same order.")]
        [SerializeField] private Key[] speedKeys = { Key.Digit1, Key.Digit2, Key.Digit3 };

        [Inject] private MuseumDataModel _model;

        private MuseumTimeData _fallback;   // used only if the scene has no data model
        private int _speed = 1;
        private bool _paused;

        public bool IsPaused => _paused;
        public int Speed => _speed;
        public MuseumTimeData Now => _model != null ? _model.Clock : _fallback ??= new MuseumTimeData();

        // ── Lifecycle ───────────────────────────────────────────────────

        private void OnEnable()
        {
            MuseumActions.OnClickPausePlayButton += TogglePause;
            MuseumActions.OnClickTimeSpeedButton += SetSpeed;
            BuilderActions.OnMuseumDataReloaded += Broadcast;
        }

        private void OnDisable()
        {
            MuseumActions.OnClickPausePlayButton -= TogglePause;
            MuseumActions.OnClickTimeSpeedButton -= SetSpeed;
            BuilderActions.OnMuseumDataReloaded -= Broadcast;
        }

        private void Start()
        {
            _model?.EnsureInitialized();
            ApplyTimeScale();
            MuseumActions.OnTimeSpeedChanged?.Invoke(_speed);
            MuseumActions.OnTimePauseValueUpdated?.Invoke(_paused);
            Broadcast();
        }

        private void OnDestroy()
        {
            // Never leave the next scene (e.g. the mine) frozen or fast-forwarded.
            Time.timeScale = 1f;
        }

        private void Update()
        {
            HandleShortcuts();
            if (_paused || realSecondsPerTenMinutes <= 0f) return;

            MuseumTimeData now = Now;
            now.Seconds += Time.unscaledDeltaTime * _speed;
            while (now.Seconds >= realSecondsPerTenMinutes)
            {
                now.Seconds -= realSecondsPerTenMinutes;
                Tick(now);
            }
        }

        // ── Controls ────────────────────────────────────────────────────

        public void TogglePause() => SetPaused(!_paused);

        public void SetPaused(bool paused)
        {
            if (_paused == paused) return;
            _paused = paused;
            ApplyTimeScale();
            MuseumActions.OnTimePauseValueUpdated?.Invoke(_paused);
        }

        public void SetSpeed(int speed)
        {
            if (System.Array.IndexOf(allowedSpeeds, speed) < 0) return;
            if (speedButtonResumes) SetPaused(false);
            if (_speed == speed) return;

            _speed = speed;
            ApplyTimeScale();
            MuseumActions.OnTimeSpeedChanged?.Invoke(_speed);
        }

        /// <summary>Jump the clock (e.g. sleeping to the next morning). Raises OnTimeUpdated.</summary>
        public void SetTime(int hours, int minutes, int days, int months, int years)
        {
            MuseumTimeData now = Now;
            now.Hours = hours;
            now.Minutes = minutes;
            now.Days = days;
            now.Months = months;
            now.Years = years;
            now.Seconds = 0f;
            Broadcast();
        }

        // ── Clock ───────────────────────────────────────────────────────

        private void Tick(MuseumTimeData t)
        {
            t.Minutes += MinutesPerTick;
            if (t.Minutes >= 60) { t.Minutes = 0; t.Hours++; }
            if (t.Hours >= HoursPerDay) { t.Hours = 0; t.Days++; }
            if (t.Days > DaysPerMonth) { t.Days = 1; t.Months++; }
            if (t.Months > MonthsPerYear) { t.Months = 1; t.Years++; }
            Broadcast();
        }

        private void Broadcast()
        {
            MuseumTimeData t = Now;
            MuseumActions.OnTimeUpdated?.Invoke(t.Minutes, t.Hours, t.Days, t.Months, t.Years);
        }

        private void ApplyTimeScale()
        {
            if (_paused && pauseFreezesGameplay) Time.timeScale = 0f;
            else Time.timeScale = speedScalesGameplay ? _speed : 1f;
        }

        private void HandleShortcuts()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null) return;

            if (pauseKey != Key.None && kb[pauseKey].wasPressedThisFrame)
                MuseumActions.OnClickPausePlayButton?.Invoke();

            for (int i = 0; i < speedKeys.Length && i < allowedSpeeds.Length; i++)
                if (speedKeys[i] != Key.None && kb[speedKeys[i]].wasPressedThisFrame)
                    MuseumActions.OnClickTimeSpeedButton?.Invoke(allowedSpeeds[i]);
        }
    }
}
