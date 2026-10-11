using System.Collections.Generic;
using UnityEngine;

namespace ProjectMuseum.Narrative
{
    /// <summary>
    /// All tutorials as an editable asset. Replaces the runtime JSON load in the
    /// Godot <c>TutorialSystem</c>. Look-ups are by <see cref="Tutorial.SceneNo"/>
    /// (the value <c>PlayTutorial</c> is invoked with).
    /// </summary>
    [CreateAssetMenu(fileName = "TutorialDatabase", menuName = "Project Museum/Tutorial Database")]
    public class TutorialDatabase : ScriptableObject
    {
        [SerializeField] private List<Tutorial> tutorials = new List<Tutorial>();

        public IReadOnlyList<Tutorial> Tutorials => tutorials;

        public Tutorial GetBySceneNo(int sceneNo)
        {
            for (int i = 0; i < tutorials.Count; i++)
                if (tutorials[i] != null && tutorials[i].SceneNo == sceneNo)
                    return tutorials[i];

            Debug.LogWarning($"[TutorialDatabase] No tutorial with SceneNo {sceneNo}.");
            return null;
        }

#if UNITY_EDITOR
        public void SetTutorials(List<Tutorial> imported) => tutorials = imported;
#endif
    }
}
