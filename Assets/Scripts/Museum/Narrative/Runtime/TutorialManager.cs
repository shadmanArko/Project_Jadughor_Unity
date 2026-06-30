using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectMuseum.Narrative
{
    /// <summary>
    /// Tracks tutorial progress. A step completes once every required keybind
    /// (WASD / scroll, tracked here via the Input System) AND every required named
    /// action (raised by other systems through
    /// <see cref="MuseumActions.OnPlayerPerformedTutorialRequiringAction"/>) has
    /// been observed. When the last step is done it optionally chains back into the
    /// story via <see cref="Tutorial.ContinuesStory"/>.
    ///
    /// Unity port of the Godot <c>TutorialSystem</c> (logic only — the on-screen
    /// panel is <see cref="TutorialPanelController"/>). Put this on a manager object
    /// and assign the <see cref="TutorialDatabase"/>.
    /// </summary>
    public class TutorialManager : MonoBehaviour
    {
        [SerializeField] private TutorialDatabase tutorialDatabase;

        private int _currentTutorialNumber;
        private int _currentTutorialSceneIndex;

        private Tutorial _currentTutorial;
        private TutorialSceneEntry _currentEntry;
        private bool _currentTutorialCompleted = true;

        private readonly HashSet<string> _performedKeyBinds = new HashSet<string>();
        private readonly HashSet<string> _performedActions = new HashSet<string>();

        // ── Lifecycle ───────────────────────────────────────────────────

        private void OnEnable()
        {
            MuseumActions.PlayTutorial += LoadTutorial;
            MuseumActions.OnPlayerPerformedTutorialRequiringAction += OnActionPerformed;
        }

        private void OnDisable()
        {
            MuseumActions.PlayTutorial -= LoadTutorial;
            MuseumActions.OnPlayerPerformedTutorialRequiringAction -= OnActionPerformed;
        }

        // ── Loading ─────────────────────────────────────────────────────

        private void LoadTutorial(int number)
        {
            if (tutorialDatabase == null)
            {
                Debug.LogError("[TutorialManager] TutorialDatabase not assigned.", this);
                return;
            }

            _currentTutorialNumber = number;
            _currentTutorial = tutorialDatabase.GetBySceneNo(number);
            if (_currentTutorial == null) return;

            // Tutorials disabled for this player → skip straight to the story.
            bool tutorialsEnabled = PlayerInfoProvider.Current?.Tutorial ?? true;
            if (!tutorialsEnabled)
            {
                if (_currentTutorial.ContinuesStory)
                    MuseumActions.PlayStoryScene?.Invoke(_currentTutorial.StoryNumber);
                return;
            }

            _currentTutorialSceneIndex = 0;
            _currentTutorialCompleted = false;
            ShowNextTutorialScene();
        }

        // ── Step flow ───────────────────────────────────────────────────

        private void ShowNextTutorialScene()
        {
            if (_currentTutorial.TutorialSceneEntries != null &&
                _currentTutorialSceneIndex < _currentTutorial.TutorialSceneEntries.Count)
            {
                _currentEntry = _currentTutorial.TutorialSceneEntries[_currentTutorialSceneIndex];
                _performedKeyBinds.Clear();
                _performedActions.Clear();
                MuseumActions.OnTutorialUpdated?.Invoke(_currentEntry.TutorialText);
                _currentTutorialSceneIndex++;

                // A step with no requirements (rare) is instantly satisfied.
                CheckForCompletion();
            }
            else
            {
                CompleteTutorial();
                if (_currentTutorial.ContinuesStory)
                    MuseumActions.PlayStoryScene?.Invoke(_currentTutorial.StoryNumber);
            }
        }

        private void CompleteTutorial()
        {
            _currentTutorialCompleted = true;
            _currentEntry = null;

            PlayerInfo info = PlayerInfoProvider.Current;
            if (info != null)
            {
                info.CompletedTutorialScene = _currentTutorialNumber;
                MuseumActions.OnPlayerInfoUpdated?.Invoke(info);
            }

            MuseumActions.OnTutorialEnded?.Invoke();
        }

        // ── Action tracking (other systems push these) ──────────────────

        private void OnActionPerformed(string action)
        {
            if (string.IsNullOrEmpty(action)) return;
            _performedActions.Add(action);
            if (!_currentTutorialCompleted) CheckForCompletion();
        }

        // ── Keybind tracking (WASD / scroll, via Input System) ──────────

        private void Update()
        {
            if (_currentTutorialCompleted || _currentEntry == null) return;
            if (_currentEntry.KeyBindsNeedsToPerform == null ||
                _currentEntry.KeyBindsNeedsToPerform.Count == 0) return;

            bool any = false;
            foreach (string bind in _currentEntry.KeyBindsNeedsToPerform)
            {
                if (_performedKeyBinds.Contains(bind)) continue;
                if (IsKeyBindPerformed(bind))
                {
                    _performedKeyBinds.Add(bind);
                    any = true;
                }
            }

            if (any && !_currentTutorialCompleted) CheckForCompletion();
        }

        /// <summary>
        /// Maps the Godot input-action names used in the tutorial data to concrete
        /// Input System checks. Extend this as new keybind-based steps are added.
        /// </summary>
        private static bool IsKeyBindPerformed(string bind)
        {
            Keyboard kb = Keyboard.current;
            Mouse mouse = Mouse.current;

            switch (bind)
            {
                case "move_left":
                    return kb != null && (kb.aKey.isPressed || kb.leftArrowKey.isPressed);
                case "move_right":
                    return kb != null && (kb.dKey.isPressed || kb.rightArrowKey.isPressed);
                case "move_up":
                    return kb != null && (kb.wKey.isPressed || kb.upArrowKey.isPressed);
                case "move_down":
                    return kb != null && (kb.sKey.isPressed || kb.downArrowKey.isPressed);
                case "ui_wheel_up":
                    return mouse != null && mouse.scroll.ReadValue().y > 0f;
                case "ui_wheel_down":
                    return mouse != null && mouse.scroll.ReadValue().y < 0f;
                default:
                    return false;
            }
        }

        // ── Completion check ────────────────────────────────────────────

        private void CheckForCompletion()
        {
            if (_currentEntry == null) return;

            if (_currentEntry.KeyBindsNeedsToPerform != null)
                foreach (string bind in _currentEntry.KeyBindsNeedsToPerform)
                    if (!_performedKeyBinds.Contains(bind)) return;

            if (_currentEntry.ActionsNeedsToPerform != null)
                foreach (string action in _currentEntry.ActionsNeedsToPerform)
                    if (!_performedActions.Contains(action)) return;

            MuseumActions.TutorialSceneEntryEnded?.Invoke(_currentEntry.EntryNo);
            ShowNextTutorialScene();
        }

        /// <summary>EntryNo of the step currently in progress (empty when none).</summary>
        public string CurrentEntryNo => _currentEntry?.EntryNo ?? string.Empty;
    }
}
