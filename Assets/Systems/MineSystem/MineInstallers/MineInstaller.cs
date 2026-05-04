using Systems.MineSystem.Mine.Config;
using Systems.MineSystem.Mine.Controller;
using Systems.MineSystem.Mine.Model;
using Systems.MineSystem.Mine.Scriptable;
using Systems.MineSystem.Mine.Service;
using Systems.MineSystem.Mine.View;
using Systems.MineSystem.MineGenerationSystem.Controller;
using Systems.MineSystem.MineGenerationSystem.Model;
using Unity.Cinemachine;
using UnityEngine;
using Zenject;

namespace Systems.MineSystem.MineInstallers
{
    [CreateAssetMenu(fileName = "MineInstaller", menuName = "Installers/MineInstaller")]
    public class MineInstaller : ScriptableObjectInstaller<MineInstaller>
    {
        [SerializeField] private Camera camera;
        [SerializeField] private CinemachineCamera cinemachineCamera;
        
        [SerializeField] private MineView mineView;
        [SerializeField] private MineGenerationConfig mineGenerationConfig;

        [SerializeField] private MineRegionalTileScriptable regionalTileScriptable;
        [SerializeField] private SpecialBackdropSpriteScriptable specialBackdropSpriteScriptable;
        
        public override void InstallBindings()
        {
            Container.Bind<Camera>().FromComponentInNewPrefab(camera).AsSingle().NonLazy();
            Container.Bind<CinemachineCamera>().FromComponentInNewPrefab(cinemachineCamera).AsSingle().NonLazy();
            
            // Config
            Container.Bind<MineGenerationConfig>().FromScriptableObject(mineGenerationConfig).AsSingle();
            
            // Scriptable
            Container.Bind<MineRegionalTileScriptable>().FromScriptableObject(regionalTileScriptable).AsSingle();
            Container.Bind<SpecialBackdropSpriteScriptable>().FromScriptableObject(specialBackdropSpriteScriptable).AsSingle();
            
            Container.BindInterfacesAndSelfTo<MineGenerationService>().AsSingle();
            Container.BindInterfacesAndSelfTo<ArtifactGenerationService>().AsSingle();
            Container.BindInterfacesAndSelfTo<CaveGenerationService>().AsSingle();
            Container.BindInterfacesAndSelfTo<ResourceGenerationService>().AsSingle();
            Container.BindInterfacesAndSelfTo<SpecialBackdropGenerationService>().AsSingle();
            Container.BindInterfacesAndSelfTo<VineGenerationService>().AsSingle();

            
            Container.BindInterfacesAndSelfTo<MineGenerationModel>().AsSingle();
            Container.BindInterfacesAndSelfTo<MineGenerationController>().AsSingle();

            
            Container.Bind<MineView>().FromComponentInNewPrefab(mineView).AsSingle();
            Container.BindInterfacesAndSelfTo<MineModel>().AsSingle();
            Container.BindInterfacesAndSelfTo<MineVisualizerService>().AsSingle();
            Container.BindInterfacesAndSelfTo<MineController>().AsSingle().NonLazy();
        }
    }
}