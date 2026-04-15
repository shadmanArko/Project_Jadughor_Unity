using System;
using Systems.MainMenuSystem.Models;
using Systems.MainMenuSystem.Views;
using UniRx;
using UniRx.Triggers;
using Zenject;

namespace Systems.MainMenuSystem.Controllers
{
    [Serializable]
    public class MainMenuController : IInitializable, IDisposable
    {
        private readonly IMainMenuModel _model;
        private readonly MainMenuView _view;

        private CompositeDisposable _disposables;

        public MainMenuController(IMainMenuModel model, MainMenuView view)
        {
            _model = model;
            _view = view;
        }

        public void Initialize()
        {
            _disposables = new CompositeDisposable();
            
            SubscribeToButtonHovers();
            SubscribeToButtonClicks();
        }

        private void SubscribeToButtonHovers()
        {
            _view.ContinueButton
                .OnMouseEnterAsObservable()
                .Subscribe(_ =>
                {
                    //TODO: Play mouse hover sfx
                }).AddTo(_disposables);
            
            _view.NewGameButton
                .OnMouseEnterAsObservable()
                .Subscribe(_ =>
                {
                    //TODO: Play mouse hover sfx
                }).AddTo(_disposables);
            
            _view.OptionsButton
                .OnMouseEnterAsObservable()
                .Subscribe(_ =>
                {
                    //TODO: Play mouse hover sfx
                }).AddTo(_disposables);
            
            _view.ExitButton
                .OnMouseEnterAsObservable()
                .Subscribe(_ =>
                {
                    //TODO: Play mouse hover sfx
                }).AddTo(_disposables);
        }

        private void SubscribeToButtonClicks()
        {
            _view.ContinueButton
                .OnClickAsObservable()
                .Subscribe(_ =>
                {
                    ContinueButtonClicked();
                }).AddTo(_disposables);
            
            _view.NewGameButton
                .OnClickAsObservable()
                .Subscribe(_ =>
                {
                    NewGameButtonClicked();
                }).AddTo(_disposables);
            
            _view.OptionsButton
                .OnClickAsObservable()
                .Subscribe(_ =>
                {
                    OptionsButtonClicked();
                }).AddTo(_disposables);
            
            _view.ExitButton
                .OnClickAsObservable()
                .Subscribe(_ =>
                {
                    ExitButtonClicked();
                }).AddTo(_disposables);
        }

        #region Mouse Clicks Functions

        private void ContinueButtonClicked()
        {
            _model.ExecuteContinue();
        }

        private void NewGameButtonClicked()
        {
            _model.ExecuteNewGame();
        }

        private void OptionsButtonClicked()
        {
            _model.ExecuteOptions();
        }

        private void ExitButtonClicked()
        {
            _model.ExecuteQuit();
        }

        #endregion

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
            _disposables?.Dispose();
        }
    }
}