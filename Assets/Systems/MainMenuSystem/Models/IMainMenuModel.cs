using System;
using UniRx;

namespace Systems.MainMenuSystem.Models
{
    public interface IMainMenuModel
    {
        IReadOnlyReactiveProperty<bool> HasSaveData { get; }

        IObservable<Unit> OnNewGameExecuted  { get; }
        IObservable<Unit> OnContinueExecuted { get; }
        IObservable<Unit> OnCreditsExecuted  { get; }
        IObservable<Unit> OnQuitExecuted     { get; }

        void ExecuteNewGame();
        void ExecuteContinue();
        void ExecuteCredits();
        void ExecuteQuit();
    }
}