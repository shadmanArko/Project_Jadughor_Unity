using System;
using ProjectMuseum.Narrative;
using Systems.MainMenuSystem.Models;
using Systems.MainMenuSystem.Views;
using UniRx;
using UnityEngine;
using UnityEngine.EventSystems;
using Zenject;

namespace Systems.MainMenuSystem.Controllers
{
    /// <summary>
    /// Port of Godot's <c>NewGameSetupUi.cs</c>.
    ///  - Shown by MainMenuController when New Game is pressed (Godot did this via
    ///    MuseumActions.OnClinkStartNewGameButton; a direct call is enough here).
    ///  - Start with an empty name → "Please Enter Your Name." warning.
    ///  - Otherwise builds a PlayerInfo (Id "string", WakeUpHour 7, ForceSleepHour 0 — Godot values),
    ///    shows the loading panel and loads the Museum scene.
    /// </summary>
    public class NewGameSetupController : IInitializable, IDisposable
    {
        private readonly IMainMenuModel _model;
        private readonly NewGameSetupView _view;
        private readonly CompositeDisposable _disposables = new();
        private IDisposable _loadingTicker;

        public NewGameSetupController(IMainMenuModel model, NewGameSetupView view)
        {
            _model = model;
            _view = view;
        }

        public void Initialize()
        {
            _view.Hide(); // Godot: NewGameSetupUi visible = false in MainMenu.tscn
            _view.SetWarningVisible(false);
            _view.SetLoadingVisible(false);

            _view.StartButton.OnClickAsObservable()
                .Subscribe(_ => OnStartPressed()).AddTo(_disposables);
            _view.WarningOkayButton.OnClickAsObservable()
                .Subscribe(_ => _view.SetWarningVisible(false)).AddTo(_disposables);

            // Enter in the name field = Start (keyboard flow); controller users
            // navigate Name → Gender → Tutorial → Start with the D-pad.
            _view.NameInput.onSubmit.AsObservable()
                .Subscribe(_ => OnStartPressed()).AddTo(_disposables);
        }

        public void Show()
        {
            _view.Show();
            EventSystem.current?.SetSelectedGameObject(_view.NameInput.gameObject);
        }

        private void OnStartPressed()
        {
            if (string.IsNullOrEmpty(_view.PlayerName))
            {
                Debug.Log("No Name");
                _view.SetWarningVisible(true);
                EventSystem.current?.SetSelectedGameObject(_view.WarningOkayButton.gameObject);
                return;
            }

            var info = new PlayerInfo
            {
                Id = "string",
                Name = _view.PlayerName,
                Gender = _view.Gender,
                Tutorial = _view.TutorialEnabled,
                WakeUpHour = 7,
                ForceSleepHour = 0
            };
            Debug.Log($"Name: {info.Name}, Gender: {info.Gender}, Tutorial: {info.Tutorial}");

            _view.SetLoadingVisible(true);
            _view.SetLoadingProgress(0);
            StartLoadingDots();

            var op = _model.StartNewGame(info);
            if (op == null) return;

            // AsyncOperation.progress tops out at 0.9 until activation; normalise.
            Observable.EveryUpdate()
                .TakeWhile(_ => op != null && !op.isDone)
                .Subscribe(_ => _view.SetLoadingProgress(op.progress / 0.9f))
                .AddTo(_disposables);
        }

        /// <summary>Godot LoadingText.cs: cycles "Loading" → "Loading..." every 0.5s.</summary>
        private void StartLoadingDots()
        {
            _loadingTicker?.Dispose();
            int dots = 0;
            _loadingTicker = Observable.Interval(TimeSpan.FromSeconds(0.5f), Scheduler.MainThreadIgnoreTimeScale)
                .Subscribe(_ =>
                {
                    dots = (dots + 1) % 4;
                    _view.SetLoadingText("Loading" + new string('.', dots));
                });
            _disposables.Add(_loadingTicker);
        }

        public void Dispose() => _disposables.Dispose();
    }
}
