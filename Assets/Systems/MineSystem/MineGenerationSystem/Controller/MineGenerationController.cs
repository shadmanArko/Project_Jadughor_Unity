using System;
using Cysharp.Threading.Tasks;
using Systems.MineSystem.Mine.Model;
using Systems.MineSystem.MineGenerationSystem.Model;
using UniRx;
using Zenject;

namespace Systems.MineSystem.MineGenerationSystem.Controller
{
    public class MineGenerationController : IInitializable, IDisposable
    {
        private CompositeDisposable _disposable;

        private MineGenerationModel _model;
        
        public void Initialize()
        {
            _disposable = new CompositeDisposable();
        }

        public async UniTask<MineData> GenerateMineData()
        {
            var mine = await _model.GenerateProceduralMineData();
            return mine;
        }

        public void Dispose()
        {
            _disposable?.Dispose();
        }
    }
}