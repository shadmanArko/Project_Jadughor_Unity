using ProjectMuseum.Narrative;
using UniRx;
using UnityEngine;

namespace Systems.MainMenuSystem.Models
{
    public interface IMainMenuModel
    {
        IReadOnlyReactiveProperty<bool> HasSaveData { get; }

        public void ExecuteNewGame();
        public void ExecuteContinue();
        public void ExecuteOptions();
        public void ExecuteQuit();

        /// <summary>
        /// Godot <c>NewGameSetupUi.StartButtonOnPressed</c>: wipe the previous save,
        /// store the new profile and start loading the Museum scene.
        /// </summary>
        AsyncOperation StartNewGame(PlayerInfo info);
    }
}
