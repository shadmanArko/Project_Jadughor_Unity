using System;
using Systems.MineSystem.MinePlayerSystem.Model;
using Systems.MineSystem.MinePlayerSystem.View;
using UniRx;
using Zenject;

namespace Systems.MineSystem.MinePlayerSystem.Controller
{
    public class MinePlayerController : IInitializable, IDisposable
    {
        private CompositeDisposable _disposable;
        
        private MinePlayerModel _model;
        private MinePlayerView _view;
        
        public void Initialize()
        {
            _disposable = new CompositeDisposable();
        }
        
        public void Dispose()
        {
            _disposable?.Dispose();
        }
    }
}