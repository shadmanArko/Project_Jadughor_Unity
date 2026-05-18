using System;
using Cysharp.Threading.Tasks;
using Systems.MineSystem.Mine.Model;
using Systems.MineSystem.Mine.Service;
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
            Debug.LogWarning($"Generating mine!");
            GenerateMine().Forget(ex => Debug.LogException(ex));
        }

        public async UniTask GenerateMine()
        {
            var mineData = await _mineGenerationController.GenerateMineData();
            Debug.LogWarning($"Mine Data Generated!");
            _model.SetMineData(mineData);
            Debug.LogWarning($"mine data set to model");
            _mineVisualizerService.GenerateMineFromData(_model.MineData.Value);
            Debug.LogWarning($"visualize mine data");
        }

        #region Hit Wall

        private Action<Vector3Int> _onWallBreak;
        public void HitWall(Vector3 position)
        {
            var cellPos = Vector3Int.RoundToInt(position);
            _onWallBreak = OnWallBreak;
            _model.HitWall(cellPos, _onWallBreak);
        }

        private void OnWallBreak(Vector3Int cellPos)
        {
            var cell = _model.MineData.Value.GetCell(cellPos);
            _mineVisualizerService.BreakWallTile(cellPos);
            _mineVisualizerService.RevealAdjacentTiles(cell);
        }

        #endregion

        public void Dispose()
        {
            _disposable?.Dispose();
        }
    }
}