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
        
        private readonly Subject<Unit> _onNewGameExecuted = new();
        private readonly Subject<Unit> _onContinueExecuted = new();
        private readonly Subject<Unit> _onCreditsExecuted = new();
        private readonly Subject<Unit> _onQuitExecuted = new();

        public IObservable<Unit> OnNewGameExecuted => _onNewGameExecuted;
        public IObservable<Unit> OnContinueExecuted => _onContinueExecuted;
        public IObservable<Unit> OnCreditsExecuted => _onCreditsExecuted;
        public IObservable<Unit> OnQuitExecuted => _onQuitExecuted;

        // ── IInitializable ────────────────────────────────────────────────
        public void Initialize()
        {
            _hasSaveData.Value = CheckForSaveData();
            Debug.Log("[Model] Initialized. HasSaveData: " + _hasSaveData.Value);
        }

        // ── IMainMenuModel ────────────────────────────────────────────────
        public void ExecuteNewGame()
        {
            Debug.Log("[Model] ExecuteNewGame.");
            // e.g. SaveSystem.ClearSession();
            _onNewGameExecuted.OnNext(Unit.Default);
        }

        public void ExecuteContinue()
        {
            // Left intentionally blank — implementation pending
        }

        public void ExecuteCredits()
        {
            Debug.Log("[Model] ExecuteCredits.");
            _onCreditsExecuted.OnNext(Unit.Default);
        }

        public void ExecuteQuit()
        {
            Debug.Log("[Model] ExecuteQuit.");
            _onQuitExecuted.OnNext(Unit.Default);
        }

        public void Dispose()
        {
            _hasSaveData.Dispose();
            _onNewGameExecuted.Dispose();
            _onContinueExecuted.Dispose();
            _onCreditsExecuted.Dispose();
            _onQuitExecuted.Dispose();

            Debug.Log("[Model] Disposed.");
        }

        // ── Private Helpers ───────────────────────────────────────────────
        private bool CheckForSaveData()
        {
            // Replace with your actual save system check
            return PlayerPrefs.HasKey("SaveExists");
        }
    }
}