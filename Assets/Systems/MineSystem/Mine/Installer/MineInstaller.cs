using Systems.MineSystem.Mine.Config;
using Systems.MineSystem.Mine.Controller;
using Systems.MineSystem.Mine.Model;
using Systems.MineSystem.Mine.Scriptable;
using Systems.MineSystem.Mine.Service;
using Systems.MineSystem.Mine.Service.CoordinateService;
using Systems.MineSystem.Mine.Service.Lighting;
using Systems.MineSystem.Mine.Service.MineArtifactService.Config;
using Systems.MineSystem.Mine.Service.MineArtifactService.Service;
using Systems.MineSystem.Mine.Service.MineArtifactService.Test;
using Systems.MineSystem.Mine.Service.MineResourceService.Config;
using Systems.MineSystem.Mine.Service.MineResourceService.Scriptable;
using Systems.MineSystem.Mine.Service.MineResourceService.Service;
using Systems.MineSystem.Mine.Service.VisualizerService;
using Systems.MineSystem.Mine.View;
using Systems.MineSystem.MineGenerationSystem.Controller;
using Systems.MineSystem.MineGenerationSystem.Model;
using Systems.Utilities.Injector;
using Systems.Utilities.ScreenShake;
using Unity.Cinemachine;
using UnityEngine;
using Zenject;

namespace Systems.MineSystem.Mine.Installer
{
    [CreateAssetMenu(fileName = "MineInstaller", menuName = "Installers/MineInstaller")]
    public class MineInstaller : ScriptableObjectInstaller<MineInstaller>
    {
        [SerializeField] private Camera camera;
        [SerializeField] private CinemachineCamera cinemachineCamera;
        
        [SerializeField] private MineView mineView;
        [SerializeField]
        private MineCoordinateCanvasView coordinateCanvasView;
        [SerializeField] private MineGenerationConfig mineGenerationConfig;
        [SerializeField] private VineConfig vineConfig;
        [SerializeField] private ArtifactGenerationConfig artifactGenerationConfig;
        [SerializeField] private ArtifactCatalogConfig artifactCatalogConfig;
        [SerializeField] private ArtifactSpriteScriptable artifactSpriteScriptable;
        [SerializeField] private ResourceGenerationConfig resourceGenerationConfig;
        [SerializeField] private ResourceSpriteScriptable resourceSpriteScriptable;
        [SerializeField] private CaveFormationConfig caveFormationConfig;
        [SerializeField] private MineLightingConfig mineLightingConfig;
        [SerializeField] private MineDarkeningConfig mineDarkeningConfig;

        [SerializeField] private MineRegionalTileScriptable regionalTileScriptable;
        [SerializeField] private SpecialBackdropSpriteScriptable specialBackdropSpriteScriptable;
        [SerializeField] private VineSpriteScriptable vineSpriteScriptable;
        [SerializeField] private CellCrackScriptable cellCrackScriptable;
        
        public override void InstallBindings()
        {
            Container.Bind<ManualInjector>().AsSingle().NonLazy();
            
            Container.Bind<Camera>().FromComponentInNewPrefab(camera).AsSingle().NonLazy();
            Container.Bind<CinemachineCamera>().FromComponentInNewPrefab(cinemachineCamera).AsSingle().NonLazy();
            Container.BindInterfacesAndSelfTo<ScreenShakeController>()
                .AsSingle()
                .NonLazy();
            
            // Config
            Container.Bind<MineGenerationConfig>().FromScriptableObject(mineGenerationConfig).AsSingle();
            Container.Bind<VineConfig>().FromScriptableObject(vineConfig).AsSingle();
            Container.Bind<ArtifactGenerationConfig>().FromScriptableObject(artifactGenerationConfig).AsSingle();
            var runtimeArtifactCatalogConfig = artifactCatalogConfig != null
                ? artifactCatalogConfig
                : CreateInstance<ArtifactCatalogConfig>();
            Container.Bind<ArtifactCatalogConfig>().FromInstance(runtimeArtifactCatalogConfig).AsSingle();
            Container.Bind<ResourceGenerationConfig>().FromScriptableObject(resourceGenerationConfig).AsSingle();
            Container.Bind<CaveFormationConfig>().FromScriptableObject(caveFormationConfig).AsSingle();
            Container.Bind<MineLightingConfig>().FromScriptableObject(mineLightingConfig).AsSingle();
            Container.Bind<MineDarkeningConfig>()
                .FromScriptableObject(mineDarkeningConfig).AsSingle();
            
            // Scriptable
            Container.Bind<MineRegionalTileScriptable>().FromScriptableObject(regionalTileScriptable).AsSingle();
            Container.Bind<SpecialBackdropSpriteScriptable>().FromScriptableObject(specialBackdropSpriteScriptable).AsSingle();
            Container.Bind<VineSpriteScriptable>().FromScriptableObject(vineSpriteScriptable).AsSingle();
            Container.Bind<CellCrackScriptable>().FromScriptableObject(cellCrackScriptable).AsSingle();
            var runtimeArtifactSpriteScriptable = artifactSpriteScriptable != null
                ? artifactSpriteScriptable
                : CreateInstance<ArtifactSpriteScriptable>();
            Container.Bind<ArtifactSpriteScriptable>().FromInstance(runtimeArtifactSpriteScriptable).AsSingle();
            
            Container.BindInterfacesAndSelfTo<MineGenerationService>().AsSingle();
            Container.BindInterfacesTo<ArtifactCatalog>().AsSingle();
            Container.BindInterfacesAndSelfTo<ArtifactGenerationService>().AsSingle();
            Container.BindInterfacesAndSelfTo<ArtifactVisualizerService>().AsSingle();
            Container.BindInterfacesAndSelfTo<CaveGenerationService>().AsSingle();
            Container.BindInterfacesAndSelfTo<ResourceGenerationService>().AsSingle();
            Container.BindInterfacesAndSelfTo<SpecialBackdropGenerationService>().AsSingle();
            Container.Bind<ResourceSpriteScriptable>().FromScriptableObject(resourceSpriteScriptable).AsSingle();
            Container.BindInterfacesAndSelfTo<ResourceVisualizerService>().AsSingle();
            Container.BindInterfacesAndSelfTo<VineGenerationService>().AsSingle();

            
            Container.BindInterfacesAndSelfTo<MineGenerationModel>().AsSingle();
            Container.BindInterfacesAndSelfTo<MineGenerationController>().AsSingle();

            Container.BindInterfacesAndSelfTo<CellCrackVisualizerService>().AsSingle();
            Container.BindInterfacesAndSelfTo<MineWallVisualizerService>().AsSingle();
            Container.BindInterfacesAndSelfTo<VineVisualizerService>().AsSingle();
            Container.BindInterfacesAndSelfTo<SpecialBackdropVisualizerService>().AsSingle();
            Container.BindInterfacesAndSelfTo<CaveFormationPool>().AsSingle();
            Container.BindInterfacesAndSelfTo<CaveVisualizerService>().AsSingle();
            Container.BindInterfacesAndSelfTo<LightingSourceManager>()
                .AsSingle()
                .NonLazy();
            Container.BindInterfacesAndSelfTo<MineDarkeningService>()
                .AsSingle().NonLazy();
            
            Container.Bind<MineView>().FromComponentInNewPrefab(mineView).AsSingle();
            Container.BindInterfacesAndSelfTo<MineModel>().AsSingle();
            Container.BindInterfacesAndSelfTo<MineController>().AsSingle().NonLazy();
            Container.Bind<MineCoordinateCanvasView>()
                .FromComponentInNewPrefab(coordinateCanvasView)
                .AsSingle()
                .NonLazy();
            Container.BindInterfacesAndSelfTo<MineCoordinateService>()
                .AsSingle()
                .NonLazy();
        }
    }
}
