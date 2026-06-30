using UnityEngine;

namespace ProjectMuseum.Narrative
{
    /// <summary>
    /// Entry point for *starting* story playback. The story now auto-chains on its
    /// own (each <see cref="StoryScene.ContinuesStory"/> plays the next via
    /// <c>DialogueManager</c>), so this isn't needed for sequential flow. Keep it
    /// for the cases where you must kick a scene off manually — e.g. when the player
    /// returns from the mine / another scene and you want to resume the next beat.
    ///
    /// Call the public methods from buttons, triggers, or other scripts:
    /// <code>
    /// FindFirstObjectByType&lt;StoryController&gt;().ResumeStory();
    /// // or
    /// storyController.PlayStory(12);
    /// </code>
    /// </summary>
    public class StoryController : MonoBehaviour
    {
        [SerializeField] private StoryDatabase storyDatabase;

        [Header("Auto-start")]
        [Tooltip("Play a scene automatically when this object starts.")]
        [SerializeField] private bool playOnStart = false;
        [Tooltip("If ON, start from PlayerInfo.CompletedStoryScene + 1 (resume). " +
                 "If OFF, start from 'Start Scene No'.")]
        [SerializeField] private bool resumeFromProgress = false;
        [SerializeField] private int startSceneNo = 1;

        private void Start()
        {
            if (!playOnStart) return;
            if (resumeFromProgress) ResumeStory();
            else PlayStory(startSceneNo);
        }

        /// <summary>Play a specific story scene by number.</summary>
        public void PlayStory(int sceneNo)
        {
            MuseumActions.PlayStoryScene?.Invoke(sceneNo);
        }

        /// <summary>
        /// Play the next scene after the player's last completed one
        /// (<see cref="PlayerInfo.CompletedStoryScene"/> + 1). Use when coming back
        /// from another scene to resume the narrative.
        /// </summary>
        public void ResumeStory()
        {
            int last = PlayerInfoProvider.Current?.CompletedStoryScene ?? 0;
            int next = last + 1;

            if (storyDatabase != null && storyDatabase.GetByScene(next) == null)
            {
                Debug.Log($"[StoryController] No story scene {next} to resume — " +
                          $"narrative may be finished (last completed {last}).");
                return;
            }

            PlayStory(next);
        }
    }
}
