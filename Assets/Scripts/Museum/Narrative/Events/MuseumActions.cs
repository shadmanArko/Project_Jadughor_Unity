using System;

namespace ProjectMuseum.Narrative
{
    /// <summary>
    /// Static event hub for the narrative (story / cutscene / tutorial) systems —
    /// the Unity port of the relevant slice of Godot's <c>MuseumActions</c>.
    ///
    /// Other gameplay systems drive tutorials by raising
    /// <see cref="OnPlayerPerformedTutorialRequiringAction"/> with the action name
    /// the tutorial step is waiting for, e.g.:
    /// <code>MuseumActions.OnPlayerPerformedTutorialRequiringAction?.Invoke("SelectItem");</code>
    ///
    /// NOTE: these are static delegates, so always unsubscribe in OnDisable/OnDestroy
    /// to avoid leaks across scene reloads (the managers here already do).
    /// </summary>
    public static class MuseumActions
    {
        // ── Story / dialogue ────────────────────────────────────────────
        /// <summary>Start playing the story scene with this SceneNo.</summary>
        public static Action<int> PlayStoryScene;
        /// <summary>A whole story scene finished (and had no tutorial to chain into).</summary>
        public static Action<int> StorySceneEnded;
        /// <summary>A single dialogue line started typing (carries EntryNo).</summary>
        public static Action<string> StorySceneEntryStarted;
        /// <summary>A single dialogue line was advanced past (carries EntryNo).</summary>
        public static Action<string> StorySceneEntryEnded;

        // ── Tutorial ────────────────────────────────────────────────────
        /// <summary>Start playing the tutorial whose SceneNo matches this number.</summary>
        public static Action<int> PlayTutorial;
        /// <summary>The current tutorial step's instruction text changed.</summary>
        public static Action<string> OnTutorialUpdated;
        /// <summary>All tutorial steps complete — hide the tutorial panel.</summary>
        public static Action OnTutorialEnded;
        /// <summary>A single tutorial step was completed (carries EntryNo).</summary>
        public static Action<string> TutorialSceneEntryEnded;
        /// <summary>
        /// Raised by gameplay systems when the player performs a named action a
        /// tutorial step may be waiting on (e.g. "SelectItem", "ClickedTownMap").
        /// </summary>
        public static Action<string> OnPlayerPerformedTutorialRequiringAction;

        // ── Town map ────────────────────────────────────────────────────
        /// <summary>The right-side Town Map button was pressed — open the town map.</summary>
        public static Action OnTownMapButtonClicked;
        /// <summary>The town map's close button (or Esc / B) was pressed — close it.</summary>
        public static Action OnClickCloseTownUi;
        /// <summary>A living house with nobody home was clicked on the town map.</summary>
        public static Action OnPlayerClickedAnEmptyHouse;

        // ── Time (Godot "TimeActions") ──────────────────────────────────
        /// <summary>The clock advanced: minutes, hours, days, months (seasons 1-4), years.</summary>
        public static Action<int, int, int, int, int> OnTimeUpdated;
        /// <summary>The pause/play button was pressed — ask the clock to toggle pause.</summary>
        public static Action OnClickPausePlayButton;
        /// <summary>The clock's paused state changed (true = paused).</summary>
        public static Action<bool> OnTimePauseValueUpdated;
        /// <summary>A speed button was pressed — ask the clock to run at this multiplier.</summary>
        public static Action<int> OnClickTimeSpeedButton;
        /// <summary>The clock's speed multiplier changed (1, 2, 4…).</summary>
        public static Action<int> OnTimeSpeedChanged;

        // ── Museum gate ─────────────────────────────────────────────────
        /// <summary>The gate sign was flipped (true = open). Guests only enter while open.</summary>
        public static Action<bool> OnClickMuseumGateToggle;

        // ── Digging & permits / map selection ───────────────────────────
        /// <summary>The right-side Digging and Permits button was pressed — open map selection.</summary>
        public static Action OnClickDiggingPermitsButton;
        /// <summary>The game is about to be saved (e.g. right before leaving for the mine).</summary>
        public static Action OnPlayerSavedGame;

        // ── Player ──────────────────────────────────────────────────────
        /// <summary>The player profile changed (e.g. completed-scene bookkeeping).</summary>
        public static Action<PlayerInfo> OnPlayerInfoUpdated;
    }
}
