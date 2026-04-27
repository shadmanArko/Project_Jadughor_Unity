using System;
using Cysharp.Threading.Tasks;
using Systems.MineSystem.Mine.Config;
using Systems.MineSystem.Mine.Model;
using Systems.MineSystem.Mine.Scriptable;
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

        private readonly SpecialBackdropSpriteScriptable _specialBackdropSpriteScriptable;

        public MineGenerationModel(
            MineGenerationConfig config, 
            MineGenerationService mineGenerationService, 
            ArtifactGenerationService artifactGenerationService, 
            CaveGenerationService caveGenerationService, 
            ResourceGenerationService resourceGenerationService, 
            SpecialBackdropGenerationService specialBackdropGenerationService, 
            VineGenerationService vineGenerationService, 
            SpecialBackdropSpriteScriptable specialBackdropSpriteScriptable)
        {
            _config = config;
            _mineGenerationService = mineGenerationService;
            _artifactGenerationService = artifactGenerationService;
            _caveGenerationService = caveGenerationService;
            _resourceGenerationService = resourceGenerationService;
            _specialBackdropGenerationService = specialBackdropGenerationService;
            _vineGenerationService = vineGenerationService;
            _specialBackdropSpriteScriptable = specialBackdropSpriteScriptable;
        }

        public void Initialize()
        {
            _disposable = new CompositeDisposable();
        }

        public async UniTask<MineData> GenerateProceduralMineData()
        {
            var mineData = await _mineGenerationService.GenerateMineCellData(_config);
            await _caveGenerationService.GenerateBossCave(_config, mineData);
            
            await _caveGenerationService.GenerateCave(_config, mineData);

            // var specialBackdrops = _specialBackdropSpriteScriptable.GetAllIds();
            // await _specialBackdropGenerationService.GenerateSpecialBackdrops(
            //     _config, mineData,specialBackdrops);
            
            
            return mineData;
        }

        public void Dispose()
        {
            _disposable.Dispose();
        }
    }
}