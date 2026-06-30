using System;
using System.Collections.Generic;

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
        public List<StorySceneEntry> StorySceneEntries = new List<StorySceneEntry>();
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
