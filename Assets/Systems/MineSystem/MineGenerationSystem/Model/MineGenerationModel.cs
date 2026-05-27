using System;
using Cysharp.Threading.Tasks;
using Systems.MineSystem.Mine.Config;
using Systems.MineSystem.Mine.Model;
using Systems.MineSystem.Mine.Scriptable;
using Systems.MineSystem.Mine.Service;
using Systems.MineSystem.MinePlayerSystem.Scriptable;
using Systems.MineSystem.ResourceSystem.Config;
using Systems.MineSystem.ResourceSystem.Service;
using UniRx;
using Zenject;

namespace Systems.MineSystem.MineGenerationSystem.Model
{
    [Serializable]
    public class MineGenerationModel : IInitializable, IDisposable
    {
        private CompositeDisposable _disposable;

        private readonly MineGenerationConfig _mineGenerationConfig;
        private readonly ResourceGenerationConfig _resourceGenerationConfig;
        
        private readonly MinePlayerScriptable _playerScriptable;

        private readonly MineGenerationService _mineGenerationService;
        private readonly ArtifactGenerationService _artifactGenerationService;
        private readonly CaveGenerationService _caveGenerationService;
        private readonly ResourceGenerationService _resourceGenerationService;
        private readonly SpecialBackdropGenerationService _specialBackdropGenerationService;
        private readonly VineGenerationService _vineGenerationService;

        private readonly SpecialBackdropSpriteScriptable _specialBackdropSpriteScriptable;

        public MineGenerationModel(
            MineGenerationConfig mineGenerationConfig,
            MineGenerationService mineGenerationService, 
            ArtifactGenerationService artifactGenerationService, 
            CaveGenerationService caveGenerationService, 
            ResourceGenerationService resourceGenerationService, 
            SpecialBackdropGenerationService specialBackdropGenerationService, 
            VineGenerationService vineGenerationService, 
            SpecialBackdropSpriteScriptable specialBackdropSpriteScriptable, 
            MinePlayerScriptable playerScriptable, 
            ResourceGenerationConfig resourceGenerationConfig)
        {
            _mineGenerationConfig = mineGenerationConfig;
            _mineGenerationService = mineGenerationService;
            _artifactGenerationService = artifactGenerationService;
            _caveGenerationService = caveGenerationService;
            _resourceGenerationService = resourceGenerationService;
            _specialBackdropGenerationService = specialBackdropGenerationService;
            _vineGenerationService = vineGenerationService;
            _specialBackdropSpriteScriptable = specialBackdropSpriteScriptable;
            _playerScriptable = playerScriptable;
            _resourceGenerationConfig = resourceGenerationConfig;
        }

        public void Initialize()
        {
            _disposable = new CompositeDisposable();
        }

        public async UniTask<MineData> GenerateProceduralMineData()
        {
            var mineData = await _mineGenerationService.GenerateMineCellData(_mineGenerationConfig);
            mineData.InitializeLookupCache();
            
            if (_mineGenerationConfig.hasBossCave)
                await _caveGenerationService.GenerateBossCave(_mineGenerationConfig, mineData);
            await _caveGenerationService.GenerateCave(_mineGenerationConfig, mineData);

            var specialBackdrops = _specialBackdropSpriteScriptable.GetAllIds(_playerScriptable.region, _playerScriptable.site);
            await _specialBackdropGenerationService.GenerateSpecialBackdrops(
                _mineGenerationConfig, mineData, specialBackdrops, 8);
            await _resourceGenerationService.GenerateResources(mineData, _resourceGenerationConfig);
            
            return mineData;
        }

        public void Dispose()
        {
            _disposable.Dispose();
        }
    }
}