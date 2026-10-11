using Systems.MineSystem.Mine.Enum;
using Systems.MineSystem.Mine.Service.MineArtifactService.Config;
using Systems.MineSystem.MinePlayerSystem.Scriptable;
using UnityEngine;

namespace ProjectMuseum.MapSelection
{
    /// <summary>
    /// Hands the chosen dig site from the museum to the mine scene.
    ///
    /// The mine already reads its region/site from two ScriptableObjects
    /// (<see cref="MinePlayerScriptable"/> and <see cref="ArtifactGenerationConfig"/>), so the
    /// choice is written straight into those assets. The static references keep the assets
    /// loaded across the scene change, so the mine's installers receive the same edited
    /// instances. No change is needed on the mine side.
    ///
    /// Note: in the Editor, writing to an asset in Play mode persists after Play stops — the
    /// assets simply keep the last site you picked.
    /// </summary>
    public static class MineSiteSelection
    {
        private static MinePlayerScriptable _player;
        private static ArtifactGenerationConfig _artifacts;

        public static bool HasSelection { get; private set; }
        public static Region Region { get; private set; }
        public static Site Site { get; private set; }

        public static void Select(Region region, Site site,
            MinePlayerScriptable player, ArtifactGenerationConfig artifacts)
        {
            Region = region;
            Site = site;
            HasSelection = true;

            _player = player;
            _artifacts = artifacts;

            if (_player != null) { _player.region = region; _player.site = site; }
            else Debug.LogWarning("[MineSiteSelection] No MinePlayerScriptable assigned — the mine keeps its old site.");

            if (_artifacts != null) { _artifacts.region = region; _artifacts.site = site; }
            else Debug.LogWarning("[MineSiteSelection] No ArtifactGenerationConfig assigned — artifacts keep the old site.");
        }
    }
}
