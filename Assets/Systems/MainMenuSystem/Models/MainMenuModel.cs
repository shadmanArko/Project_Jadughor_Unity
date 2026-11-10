using System;
using System.IO;
using ProjectMuseum.Data;
using ProjectMuseum.Narrative;
using UniRx;
using UnityEngine;
using UnityEngine.SceneManagement;
using Zenject;

namespace Systems.MainMenuSystem.Models
{
    [Serializable]
    public class MainMenuModel : IMainMenuModel, IInitializable, IDisposable
    {
        private readonly ReactiveProperty<bool> _hasSaveData = new(false);
        public IReadOnlyReactiveProperty<bool> HasSaveData => _hasSaveData;

        private readonly string _museumSceneName;

        public MainMenuModel([Inject(Id = "MuseumSceneName")] string museumSceneName)
        {
            _museumSceneName = museumSceneName;
        }

        // ── IInitializable ────────────────────────────────────────────────
        public void Initialize()
        {
            _hasSaveData.Value = CheckForSaveData();
            Debug.Log("[Model] Initialized. HasSaveData: " + _hasSaveData.Value);
        }

        public void ExecuteNewGame()
        {
            // The setup screen (NewGameSetupController) is shown by the controller;
            // nothing is wiped until the player presses Start (same as Godot).
            Debug.Log("[Model] Execute NewGame → setup screen");
        }

        public void ExecuteContinue()
        {
            // MuseumDataModel loads museumData.json and PlayerInfoProvider loads
            // playerInfo.json when the Museum scene boots.
            SceneManager.LoadSceneAsync(_museumSceneName);
        }

        public void ExecuteOptions()
        {
            Debug.Log("[Model] Execute Options (settings panel not ported yet)");
        }

        public void ExecuteQuit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        public AsyncOperation StartNewGame(PlayerInfo info)
        {
            // Godot LoadDataForNewGame(): fresh SaveData built from "Starting Data".
            // Unity equivalent: delete the museum save so MuseumDataModel runs
            // InitializeNewGame() from its MuseumDataAsset on the next boot.
            if (File.Exists(MuseumDataModel.SavePath)) File.Delete(MuseumDataModel.SavePath);

            PlayerProfileStore.Save(info);
            PlayerInfoProvider.SetProfile(info);
            _hasSaveData.Value = true;

            return SceneManager.LoadSceneAsync(_museumSceneName);
        }

        public void Dispose()
        {
            _hasSaveData.Dispose();
        }

        private static bool CheckForSaveData() =>
            PlayerProfileStore.Exists || File.Exists(MuseumDataModel.SavePath);
    }
}
