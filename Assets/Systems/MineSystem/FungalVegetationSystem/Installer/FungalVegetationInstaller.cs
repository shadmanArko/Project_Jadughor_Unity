using Systems.MineSystem.FungalVegetationSystem.Config;
using Systems.MineSystem.FungalVegetationSystem.Controller;
using Systems.MineSystem.FungalVegetationSystem.Model;
using Systems.MineSystem.FungalVegetationSystem.Service;
using UnityEngine;
using Zenject;

namespace Systems.MineSystem.FungalVegetationSystem.Installer
{
    /// <summary>
    /// Add this to the Mine scene's SceneContext installer list AFTER MineInstaller and
    /// DayAndTimeInstaller - it injects MineView, MineModel and DayAndTimeConfig from those.
    /// </summary>
    [CreateAssetMenu(
        fileName = "FungalVegetationInstaller",
        menuName = "Installers/FungalVegetationInstaller")]
    public class FungalVegetationInstaller
        : ScriptableObjectInstaller<FungalVegetationInstaller>
    {
        [SerializeField] private FungalVegetationConfig fungalVegetationConfig;

        public override void InstallBindings()
        {
            Container.Bind<FungalVegetationConfig>()
                .FromScriptableObject(fungalVegetationConfig)
                .AsSingle();

            Container.BindInterfacesAndSelfTo<FungalTileCacheService>().AsSingle();
            Container.BindInterfacesAndSelfTo<FungalGrowthPlacementService>().AsSingle();
            Container.BindInterfacesAndSelfTo<FungalVegetationModel>().AsSingle();

            // NonLazy is mandatory: nothing else depends on the controller, so without it
            // Zenject never constructs it and Initialize never runs. Same reason
            // MineDarkeningService and LightingSourceManager are NonLazy.
            Container.BindInterfacesAndSelfTo<FungalVegetationController>()
                .AsSingle()
                .NonLazy();
        }
    }
}
