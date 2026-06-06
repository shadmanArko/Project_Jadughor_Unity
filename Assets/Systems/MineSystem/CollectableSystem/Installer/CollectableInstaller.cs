using Systems.MineSystem.CollectableSystem.Controller;
using Systems.MineSystem.CollectableSystem.Interface;
using Systems.MineSystem.CollectableSystem.Scriptable;
using Systems.MineSystem.CollectableSystem.Service;
using Systems.MineSystem.CollectableSystem.View;
using UnityEngine;
using Zenject;

namespace Systems.MineSystem.CollectableSystem.Installer
{
    [CreateAssetMenu(fileName = "CollectableInstaller", menuName = "Installers/CollectableInstaller")]
    public sealed class CollectableInstaller : ScriptableObjectInstaller<CollectableInstaller>
    {
        [SerializeField] private CollectableSystemConfig config;
        [SerializeField] private CellPlaceableSpriteScriptable cellPlaceableSprites;
        [SerializeField] private WallPlaceableSpriteScriptable wallPlaceableSprites;

        public override void InstallBindings()
        {
            Container.Bind<CollectableSystemConfig>()
                .FromScriptableObject(config)
                .AsSingle();
            Container.Bind<CellPlaceableSpriteScriptable>()
                .FromScriptableObject(cellPlaceableSprites)
                .AsSingle();
            Container.Bind<WallPlaceableSpriteScriptable>()
                .FromScriptableObject(wallPlaceableSprites)
                .AsSingle();

            Container.Bind<CollectorRegistry>().AsSingle();
            Container.Bind<CollectableSpriteResolver>().AsSingle();
            Container.Bind<CollectableFactory>().AsSingle();

            Container.Bind<ICollectableSpriteProvider>()
                .To<ResourceCollectableSpriteProvider>().AsSingle();
            Container.Bind<ICollectableSpriteProvider>()
                .To<ArtifactCollectableSpriteProvider>().AsSingle();
            Container.Bind<ICollectableSpriteProvider>()
                .To<CellPlaceableCollectableSpriteProvider>().AsSingle();
            Container.Bind<ICollectableSpriteProvider>()
                .To<WallPlaceableCollectableSpriteProvider>().AsSingle();

            BindPools();

            Container.Bind<ICollectablePoolHandler>()
                .To<ResourceCollectablePoolHandler>().AsSingle();
            Container.Bind<ICollectablePoolHandler>()
                .To<ArtifactCollectablePoolHandler>().AsSingle();
            Container.Bind<ICollectablePoolHandler>()
                .To<CellPlaceableCollectablePoolHandler>().AsSingle();
            Container.Bind<ICollectablePoolHandler>()
                .To<WallPlaceableCollectablePoolHandler>().AsSingle();

            Container.BindInterfacesAndSelfTo<CollectableController>()
                .AsSingle();
            Container.BindInterfacesAndSelfTo<MineItemReleaseService>()
                .AsSingle();

            Container.Bind<DummyCollectorView>()
                .FromComponentInNewPrefab(config.dummyCollectorPrefab)
                .AsSingle()
                .NonLazy();
            Container.BindInterfacesAndSelfTo<DummyCollectorController>()
                .AsSingle()
                .NonLazy();
        }

        private void BindPools()
        {
            Container.BindMemoryPool<CollectableView, ResourceCollectablePool>()
                .WithInitialSize(config.resourceInitialSize)
                .WithMaxSize(config.resourceMaxSize)
                .ExpandByOneAtATime()
                .FromComponentInNewPrefab(config.resourceCollectablePrefab)
                .UnderTransformGroup("Resource Collectables");

            Container.BindMemoryPool<CollectableView, ArtifactCollectablePool>()
                .WithInitialSize(config.artifactInitialSize)
                .WithMaxSize(config.artifactMaxSize)
                .ExpandByOneAtATime()
                .FromComponentInNewPrefab(config.artifactCollectablePrefab)
                .UnderTransformGroup("Artifact Collectables");

            Container.BindMemoryPool<CollectableView, CellPlaceableCollectablePool>()
                .WithInitialSize(config.cellPlaceableInitialSize)
                .WithMaxSize(config.cellPlaceableMaxSize)
                .ExpandByOneAtATime()
                .FromComponentInNewPrefab(config.cellPlaceableCollectablePrefab)
                .UnderTransformGroup("Cell Placeable Collectables");

            Container.BindMemoryPool<CollectableView, WallPlaceableCollectablePool>()
                .WithInitialSize(config.wallPlaceableInitialSize)
                .WithMaxSize(config.wallPlaceableMaxSize)
                .ExpandByOneAtATime()
                .FromComponentInNewPrefab(config.wallPlaceableCollectablePrefab)
                .UnderTransformGroup("Wall Placeable Collectables");
        }
    }
}