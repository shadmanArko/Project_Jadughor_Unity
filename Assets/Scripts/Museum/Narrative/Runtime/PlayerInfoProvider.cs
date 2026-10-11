using UnityEngine;

namespace ProjectMuseum.Narrative
{
    /// <summary>
    /// In-memory replacement for Godot's <c>SaveLoadService</c>/HTTP player profile.
    /// Holds the single <see cref="PlayerInfo"/> the narrative system reads, and
    /// keeps it in sync with <see cref="MuseumActions.OnPlayerInfoUpdated"/>.
    ///
    /// Put one of these in the scene (it survives load via DontDestroyOnLoad) so
    /// <see cref="Current"/> is available before any manager runs. When you build a
    /// real save system, feed the loaded profile in through <see cref="SetProfile"/>.
    /// </summary>
    [DefaultExecutionOrder(-100)] // initialise before the managers' OnEnable/Start
    public class PlayerInfoProvider : MonoBehaviour
    {
        [Header("Initial profile (used when no save system is present)")]
        [SerializeField] private string playerName = "Player";
        [SerializeField] private string gender = "Male";
        [Tooltip("Master switch — when OFF, tutorials are skipped and the story is " +
                 "played straight through (mirrors PlayerInfo.Tutorial in Godot).")]
        [SerializeField] private bool tutorialsEnabled = true;

        /// <summary>The live player profile. Never null after Awake.</summary>
        public static PlayerInfo Current { get; private set; } = new PlayerInfo();

        private void Awake()
        {
            Current = new PlayerInfo
            {
                Name = playerName,
                Gender = gender,
                Tutorial = tutorialsEnabled
            };

            DontDestroyOnLoad(gameObject);
            MuseumActions.OnPlayerInfoUpdated += OnPlayerInfoUpdated;
        }

        private void OnDestroy()
        {
            MuseumActions.OnPlayerInfoUpdated -= OnPlayerInfoUpdated;
        }

        private static void OnPlayerInfoUpdated(PlayerInfo info)
        {
            if (info != null) Current = info;
        }

        /// <summary>Replace the active profile and notify listeners.</summary>
        public static void SetProfile(PlayerInfo info)
        {
            if (info == null) return;
            Current = info;
            MuseumActions.OnPlayerInfoUpdated?.Invoke(info);
        }
    }
}
