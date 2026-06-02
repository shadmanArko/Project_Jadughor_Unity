using System;
using Cysharp.Threading.Tasks;
using Systems.MineSystem.Mine.Model;
using Systems.MineSystem.Mine.Service;
using Systems.MineSystem.Mine.Service.VisualizerService;
using Systems.MineSystem.Mine.View;
using Systems.MineSystem.MineGenerationSystem.Controller;
using UniRx;
using UnityEngine;
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
        private readonly MineWallVisualizerService _mineWallVisualizerService;

        public MineController(
            MineModel model, 
            MineView view, 
            MineGenerationController mineGenerationController, 
            MineWallVisualizerService mineWallVisualizerService)
        {
            _model = model;
            _view = view;
            _mineGenerationController = mineGenerationController;
            _mineWallVisualizerService = mineWallVisualizerService;
        }

        public void Initialize()
        {
            _disposable = new CompositeDisposable();
            GenerateMine().Forget(ex => Debug.LogException(ex));
        }

        public async UniTask GenerateMine()
        {
            var mineData = await _mineGenerationController.GenerateMineData();
            _model.SetMineData(mineData);
            _model.GenerateMineFromData();
        }

        #region Hit Wall
        
        public void HitWall(Vector3 position)
        {
            var cellPos = Vector3Int.RoundToInt(position);
            _model.HitCell(cellPos);
        }

        #endregion

        public void Dispose()
        {
            _disposable?.Dispose();
        }
    }
}