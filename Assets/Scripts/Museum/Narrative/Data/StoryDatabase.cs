using System.Collections.Generic;
using UnityEngine;

namespace ProjectMuseum.Narrative
{
    /// <summary>
    /// All story scenes as an editable asset. Replaces the runtime JSON load in
    /// the Godot <c>DialogueSystem</c> — populate it once from the source JSON via
    /// <c>Tools ▸ Project Museum ▸ Import Narrative JSON</c>, then edit in the
    /// Inspector. Look-ups are by <see cref="StoryScene.SceneNo"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "StoryDatabase", menuName = "Project Museum/Story Database")]
    public class StoryDatabase : ScriptableObject
    {
        [SerializeField] private List<StoryScene> scenes = new List<StoryScene>();

        public IReadOnlyList<StoryScene> Scenes => scenes;

        public StoryScene GetByScene(int sceneNo)
        {
            for (int i = 0; i < scenes.Count; i++)
                if (scenes[i] != null && scenes[i].SceneNo == sceneNo)
                    return scenes[i];

            Debug.LogWarning($"[StoryDatabase] No story scene with SceneNo {sceneNo}.");
            return null;
        }

#if UNITY_EDITOR
        /// <summary>Editor-only: replace the contents from the importer.</summary>
        public void SetScenes(List<StoryScene> imported) => scenes = imported;
#endif
    }
}
