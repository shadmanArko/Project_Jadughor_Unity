using System;
using Cysharp.Threading.Tasks;
using Systems.MineSystem.Mine.Model;
using Systems.MineSystem.Mine.Service;
using Systems.MineSystem.Mine.View;
using Systems.MineSystem.MineGenerationSystem.Controller;
using UniRx;
using Zenject;

namespace Systems.MineSystem.Mine.Controller
{
    [Serializable]
    public class MineController : IInitializable, IDisposable
    {
        private CompositeDisposable _disposable;

        private MineModel _model;
        private MineView _view;

        private readonly MineGenerationController _mineGenerationController;
        private readonly MineVisualizerService _mineVisualizerService;

        public MineController(
            MineModel model, 
            MineView view, 
            MineGenerationController mineGenerationController, 
            MineVisualizerService mineVisualizerService)
        {
            _model = model;
            _view = view;
            _mineGenerationController = mineGenerationController;
            _mineVisualizerService = mineVisualizerService;
        }

        public void Initialize()
        {
            _disposable = new CompositeDisposable();
            
            GenerateMine().Forget();
        }

        public async UniTask GenerateMine()
        {
            var mineData = await _mineGenerationController.GenerateMineData();
            _model.SetMineData(mineData);
            _mineVisualizerService.GenerateMineFromData(_model.MineData.Value);
        }

        public void Dispose()
        {
            _disposable?.Dispose();
        }
    }
}