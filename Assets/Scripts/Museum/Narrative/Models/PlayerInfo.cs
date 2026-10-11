using System;

namespace ProjectMuseum.Narrative
{
    /// <summary>
    /// Runtime player profile. In Godot this came from the save file / HTTP API;
    /// here it lives in memory (see <c>PlayerInfoProvider</c>) since we are not
    /// porting the backend. Only the fields the narrative system needs are used,
    /// but the full shape is kept for parity with the Godot model.
    /// </summary>
    [Serializable]
    public class PlayerInfo
    {
        public string Id;
        public string Name = "Player";
        public string Gender = "Male";

        /// <summary>Master switch: are tutorials enabled for this player?</summary>
        public bool Tutorial = true;

        public int CompletedStoryScene;
        public int CompletedTutorialScene;
        public int WakeUpHour;
        public int ForceSleepHour;
    }
}
