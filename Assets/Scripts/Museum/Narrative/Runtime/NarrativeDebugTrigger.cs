using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectMuseum.Narrative
{
    /// <summary>
    /// Test helper — kicks off a story scene so you can exercise the dialogue →
    /// cutscene → tutorial → story chain without the rest of the game. Optionally
    /// plays on Start, and lets you fire a story scene, a tutorial, or the current
    /// tutorial step's required actions from the keyboard. Remove from production.
    /// </summary>
    public class NarrativeDebugTrigger : MonoBehaviour
    {
        [Header("Auto-play")]
        [SerializeField] private bool playStorySceneOnStart = true;
        [SerializeField] private int storySceneOnStart = 1;

        [Header("Manual triggers")]
        [Tooltip("Press to (re)play 'Story Scene To Play'.")]
        [SerializeField] private Key playStoryKey = Key.F1;
        [SerializeField] private int storySceneToPlay = 1;
        [Tooltip("Press to play 'Tutorial To Play' directly.")]
        [SerializeField] private Key playTutorialKey = Key.F2;
        [SerializeField] private int tutorialToPlay = 1;

        [Header("Action stepping")]
        [Tooltip("Press to perform the current tutorial step's next required action. " +
                 "Each press fires the next one automatically — no need to type names. " +
                 "Falls back to 'Fallback Action' if no tutorial step is active.")]
        [SerializeField] private Key fireActionKey = Key.F3;
        [Tooltip("Used only when no tutorial step is currently awaiting an action.")]
        [SerializeField] private string fallbackAction = "SelectItem";

        [Header("References (auto-found if left empty)")]
        [SerializeField] private TutorialManager tutorialManager;

        private void Awake()
        {
            if (tutorialManager == null)
                tutorialManager = FindFirstObjectByType<TutorialManager>();
        }

        private void Start()
        {
            if (playStorySceneOnStart)
                MuseumActions.PlayStoryScene?.Invoke(storySceneOnStart);
        }

        private void Update()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null) return;

            if (kb[playStoryKey].wasPressedThisFrame)
                MuseumActions.PlayStoryScene?.Invoke(storySceneToPlay);

            if (kb[playTutorialKey].wasPressedThisFrame)
                MuseumActions.PlayTutorial?.Invoke(tutorialToPlay);

            if (kb[fireActionKey].wasPressedThisFrame)
                FireNextAction();
        }

        /// <summary>
        /// Performs the current tutorial step's next pending required action. Pressing
        /// the key repeatedly walks through every required action of the step in order,
        /// which is enough to complete action-based steps without any other systems.
        /// </summary>
        private void FireNextAction()
        {
            string action = tutorialManager != null ? tutorialManager.NextPendingAction() : null;
            if (string.IsNullOrEmpty(action)) action = fallbackAction;
            if (string.IsNullOrEmpty(action)) return;

            Debug.Log($"[NarrativeDebugTrigger] Firing tutorial action: {action}");
            MuseumActions.OnPlayerPerformedTutorialRequiringAction?.Invoke(action);
        }
    }
}
