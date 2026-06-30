using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectMuseum.Narrative
{
    /// <summary>
    /// Test helper — kicks off a story scene so you can exercise the dialogue →
    /// cutscene → tutorial → story chain without the rest of the game. Optionally
    /// plays on Start, and lets you fire a story scene, a tutorial, or a named
    /// tutorial action from the keyboard. Remove from production scenes.
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
        [Tooltip("Press to raise 'Action To Fire' (simulates another system).")]
        [SerializeField] private Key fireActionKey = Key.F3;
        [SerializeField] private string actionToFire = "SelectItem";

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
                MuseumActions.OnPlayerPerformedTutorialRequiringAction?.Invoke(actionToFire);
        }
    }
}
