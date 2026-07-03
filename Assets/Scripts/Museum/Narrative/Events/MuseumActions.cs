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

        // ── Player ──────────────────────────────────────────────────────
        /// <summary>The player profile changed (e.g. completed-scene bookkeeping).</summary>
        public static Action<PlayerInfo> OnPlayerInfoUpdated;
    }
}
