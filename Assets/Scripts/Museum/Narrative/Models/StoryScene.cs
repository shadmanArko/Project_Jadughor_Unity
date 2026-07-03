using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectMuseum.Narrative
{
    /// <summary>
    /// A single story scene (a run of dialogue lines). Mirrors the Godot/ASP.NET
    /// <c>StoryScene</c> model so the same JSON deserialises 1:1.
    /// Field names are PascalCase on purpose — they must match the JSON keys for
    /// Unity's <see cref="UnityEngine.JsonUtility"/> to map them.
    /// </summary>
    [Serializable]
    public class StoryScene
    {
        public string Id;
        public int SceneNo;
        public bool HasTutorial;
        public int TutorialNumber;
        
        public bool ContinuesStory;
        [Tooltip("Which scene to continue to. Leave 0 to default to SceneNo + 1.")]
        public int NextStoryNumber;

        public List<StorySceneEntry> StorySceneEntries = new List<StorySceneEntry>();

        /// <summary>The scene to chain into: explicit NextStoryNumber, else SceneNo+1.</summary>
        public int ResolvedNextStoryNumber => NextStoryNumber > 0 ? NextStoryNumber : SceneNo + 1;
    }

    /// <summary>One spoken line within a <see cref="StoryScene"/>.</summary>
    [Serializable]
    public class StorySceneEntry
    {
        public string EntryNo;
        public string IllustrationName;
        public string Dialogue;
        public string Speaker;
        public string SpeakerEmotion;
        public bool HasCutscene;
        public bool HasCutsceneArt;
    }
}
