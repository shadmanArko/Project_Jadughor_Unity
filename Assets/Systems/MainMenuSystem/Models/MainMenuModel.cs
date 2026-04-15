using System;
using UniRx;
using UnityEngine;
using Zenject;

namespace Systems.MainMenuSystem.Models
{
    [Serializable]
    public class MainMenuModel : IMainMenuModel, IInitializable, IDisposable
    {
        private readonly ReactiveProperty<bool> _hasSaveData = new(false);
        public IReadOnlyReactiveProperty<bool> HasSaveData => _hasSaveData;

        // ── IInitializable ────────────────────────────────────────────────
        public void Initialize()
        {
            _hasSaveData.Value = CheckForSaveData();
            Debug.Log("[Model] Initialized. HasSaveData: " + _hasSaveData.Value);
        }
        
        public void ExecuteNewGame()
        {
            Debug.Log($"Execute NewGame");
            //TODO: reset running data to default and start new game
        }

        public void ExecuteContinue()
        {
            Debug.Log($"Execute Continue");
            //TODO: check for saves of the game and load them
        }

        public void ExecuteOptions()
        {
            Debug.Log($"Execute Credits");
        }

        public void ExecuteQuit()
        {
            Debug.Log("[Model] ExecuteQuit.");
        }

        public void Dispose()
        {
            _hasSaveData.Dispose();
            Debug.Log("[Model] Disposed.");
        }
        
        private bool CheckForSaveData()
        {
            return true;
        }
    }
}