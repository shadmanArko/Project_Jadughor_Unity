using System;
using System.IO;
using UnityEngine;

namespace ProjectMuseum.Narrative
{
    /// <summary>
    /// Disk persistence for <see cref="PlayerInfo"/> (name, gender, tutorial switch,
    /// story/tutorial progress). Replaces the PlayerInfo half of Godot's
    /// <c>SaveLoadService.Save(saveData)</c>. Lives next to <c>museumData.json</c>
    /// in <see cref="Application.persistentDataPath"/>.
    ///
    /// Written by the New Game setup screen, read by <see cref="PlayerInfoProvider"/>
    /// when the Museum scene boots, and re-written whenever the narrative system
    /// raises <see cref="MuseumActions.OnPlayerInfoUpdated"/>.
    /// </summary>
    public static class PlayerProfileStore
    {
        public static string SavePath => Path.Combine(Application.persistentDataPath, "playerInfo.json");

        public static bool Exists => File.Exists(SavePath);

        public static void Save(PlayerInfo info)
        {
            if (info == null) return;
            try
            {
                File.WriteAllText(SavePath, JsonUtility.ToJson(info, true));
            }
            catch (Exception e)
            {
                Debug.LogError($"[PlayerProfileStore] Save failed: {e.Message}");
            }
        }

        public static bool TryLoad(out PlayerInfo info)
        {
            info = null;
            if (!Exists) return false;
            try
            {
                info = JsonUtility.FromJson<PlayerInfo>(File.ReadAllText(SavePath));
                return info != null;
            }
            catch (Exception e)
            {
                Debug.LogError($"[PlayerProfileStore] Load failed: {e.Message}");
                return false;
            }
        }

        public static void Delete()
        {
            if (Exists) File.Delete(SavePath);
        }
    }
}
