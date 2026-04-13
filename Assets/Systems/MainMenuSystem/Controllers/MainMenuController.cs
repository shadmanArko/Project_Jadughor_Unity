using System;
using Systems.MainMenuSystem.Models;
using Systems.MainMenuSystem.Views;
using UniRx;
using Zenject;

namespace Systems.MainMenuSystem.Controllers
{
    [Serializable]
    public class MainMenuController : IMainMenuController, IInitializable, IDisposable
    {
        // ── Injected Dependencies (via constructor injection) ─────────────
        private readonly IMainMenuModel _model;
        private readonly MainMenuView _view;

        private readonly CompositeDisposable _disposables = new();

        public void Initialize()
        {
            _view.ContinueButton.OnClickAsObservable()
                .Subscribe(_ =>
                {
                    ContinueButtonClicked();
                }).AddTo(_disposables);
        }

        private void ContinueButtonClicked()
        {
            
        }

        #region Sfx

        private void PlayHoverSfx()
        {
            
        }

        private void PlayClickSfx()
        {
            
        }

        #endregion

        public void Dispose()
        {
            
        }
    }
}