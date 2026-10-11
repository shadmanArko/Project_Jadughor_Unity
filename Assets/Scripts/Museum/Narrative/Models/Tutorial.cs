using System;
using System.Collections.Generic;

namespace ProjectMuseum.Narrative
{
    /// <summary>
    /// A tutorial bound to a story beat. When its steps are all completed it can
    /// hand control back to the dialogue system via <see cref="ContinuesStory"/>.
    /// Mirrors the Godot/ASP.NET <c>Tutorial</c> model (PascalCase = JSON keys).
    /// </summary>
    [Serializable]
    public class Tutorial
    {
        public string Id;
        public int SceneNo;
        public bool ContinuesStory;
        public int StoryNumber;
        public List<TutorialSceneEntry> TutorialSceneEntries = new List<TutorialSceneEntry>();
    }

    /// <summary>
    /// One tutorial step. It is considered complete only when every keybind in
    /// <see cref="KeyBindsNeedsToPerform"/> AND every action in
    /// <see cref="ActionsNeedsToPerform"/> has been observed.
    /// </summary>
    [Serializable]
    public class TutorialSceneEntry
    {
        public string EntryNo;
        public string TutorialText;
        public List<string> KeyBindsNeedsToPerform = new List<string>();
        public List<string> ActionsNeedsToPerform = new List<string>();
    }
}
