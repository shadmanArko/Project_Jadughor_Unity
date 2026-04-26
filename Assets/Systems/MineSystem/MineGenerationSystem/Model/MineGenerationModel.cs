using System;
using Cysharp.Threading.Tasks;
using Systems.MineSystem.Mine.Config;
using Systems.MineSystem.Mine.Model;
using Systems.MineSystem.Mine.Service;
using UniRx;
using Zenject;

namespace Systems.MineSystem.MineGenerationSystem.Model
{
    [Serializable]
    public class MineGenerationModel : IInitializable, IDisposable
    {
        private CompositeDisposable _disposable;

        private readonly MineGenerationConfig _config;

        private readonly MineGenerationService _mineGenerationService;
        private readonly ArtifactGenerationService _artifactGenerationService;
        private readonly CaveGenerationService _caveGenerationService;
        private readonly ResourceGenerationService _resourceGenerationService;
        private readonly SpecialBackdropGenerationService _specialBackdropGenerationService;
        private readonly VineGenerationService _vineGenerationService;

        public MineGenerationModel(
            MineGenerationConfig config, 
            MineGenerationService mineGenerationService, 
            ArtifactGenerationService artifactGenerationService, 
            CaveGenerationService caveGenerationService, 
            ResourceGenerationService resourceGenerationService, 
            SpecialBackdropGenerationService specialBackdropGenerationService, 
            VineGenerationService vineGenerationService)
        {
            _config = config;
            _mineGenerationService = mineGenerationService;
            _artifactGenerationService = artifactGenerationService;
            _caveGenerationService = caveGenerationService;
            _resourceGenerationService = resourceGenerationService;
            _specialBackdropGenerationService = specialBackdropGenerationService;
            _vineGenerationService = vineGenerationService;
        }

        public void Initialize()
        {
            _disposable = new CompositeDisposable();
        }

        public async UniTask<MineData> GenerateProceduralMine()
        {
            var mineData = await _mineGenerationService.GenerateMineCellData(_config);
            //TODO: complete mine generation
            return mineData;
        }

        private async UniTask GenerateMineCells()
        {
            
        }
        
        

        public void Dispose()
        {
            _disposable.Dispose();
        }
    }
}