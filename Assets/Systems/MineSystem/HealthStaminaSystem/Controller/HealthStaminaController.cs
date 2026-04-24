using System;
using Systems.MineSystem.HealthStaminaSystem.Model;
using Systems.MineSystem.HealthStaminaSystem.View;
using UniRx;
using Zenject;

namespace Systems.MineSystem.HealthStaminaSystem.Controller
{
    [Serializable]
    public class HealthStaminaController : IInitializable, IDisposable
    {
        private CompositeDisposable _disposables;

        private HealthStaminaModel _model;
        private HealthStaminaCanvasView _view;

        public HealthStaminaController(
            HealthStaminaModel model, 
            HealthStaminaCanvasView view)
        {
            _disposables = new CompositeDisposable();
            _model = model;
            _view = view;
        }
        
        public void Initialize()
        {
            SubscribeToProperties();
        }

        private void SubscribeToProperties()
        {
            _model.MaxHealth
                .Subscribe(value =>
                {
                    _view.healthSlider.maxValue = value;
                }).AddTo(_disposables);

            _model.MaxStamina
                .Subscribe(value =>
                {
                    _view.staminaSlider.maxValue = value;
                }).AddTo(_disposables);
            
            _model.Health
                .Subscribe(value =>
                {
                      _view.healthSlider.value = value;
                }).AddTo(_disposables);

            _model.Stamina
                .Subscribe(value =>
                {
                    _view.staminaSlider.value = value;
                }).AddTo(_disposables);
        }

        #region Health

        public void IncreaseHealth(float value)
        {
            _model.IncreaseHealth(value);
        }

        public void ReduceHealth(float value)
        {
            _model.ReduceHealth(value);
        }
        

        #endregion

        #region Stamina

        public void IncreaseStamina(float value)
        {
            _model.IncreaseStamina(value);
        }

        public void ReduceStamina(float value)
        {
            _model.ReduceStamina(value);
        }

        #endregion
        
        public void Dispose()
        {
            _disposables?.Dispose();
            _model?.Dispose();
        }
    }
}
