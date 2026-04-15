using System;
using UniRx;

namespace Systems.MainMenuSystem.Models
{
    public interface IMainMenuModel
    {
        IReadOnlyReactiveProperty<bool> HasSaveData { get; }

        public void ExecuteNewGame();
        public void ExecuteContinue();
        public void ExecuteOptions();
        public void ExecuteQuit();
    }
}