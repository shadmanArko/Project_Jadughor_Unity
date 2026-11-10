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
        private readonly NewGameSetupController _newGameSetup;

        private CompositeDisposable _disposables;

        public MainMenuController(IMainMenuModel model, MainMenuView view, NewGameSetupController newGameSetup)
        {
            _model = model;
            _view = view;
            _newGameSetup = newGameSetup;
        }

        public void Initialize()
        {
            _disposables = new CompositeDisposable();
            
            SubscribeToButtonHovers();
            SubscribeToButtonClicks();

            // Continue is only usable once a save exists (Godot always enabled it and
            // relied on the backend; here there is nothing to continue without a save).
            _model.HasSaveData
                .Subscribe(has => _view.ContinueButton.interactable = has)
                .AddTo(_disposables);
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
            // Godot MainMenu.NewGameButtonOnPressed: hide menu, show setup screen.
            _view.Hide();
            _newGameSetup.Show();
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