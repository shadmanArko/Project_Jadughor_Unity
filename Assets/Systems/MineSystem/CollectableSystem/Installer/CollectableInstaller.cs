using Systems.MineSystem.CollectableSystem.Controller;
using Systems.MineSystem.CollectableSystem.Interface;
using Systems.MineSystem.CollectableSystem.Scriptable;
using Systems.MineSystem.CollectableSystem.Service;
using Systems.MineSystem.CollectableSystem.Service.CollectableSpriteProviders;
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
                .To<CommonCollectablePoolHandler>().AsSingle();

            Container.BindInterfacesAndSelfTo<CollectableController>()
                .AsSingle();
            Container.BindInterfacesAndSelfTo<MineItemReleaseService>()
                .AsSingle();
        }

        private void BindPools()
        {
            if (config.maximumPoolSize > 0)
            {
                Container
                    .BindMemoryPool<CollectableView, CommonCollectablePool>()
                    .WithInitialSize(config.initialPoolSize)
                    .WithMaxSize(config.maximumPoolSize)
                    .ExpandByOneAtATime()
                    .FromComponentInNewPrefab(config.commonCollectablePrefab)
                    .UnderTransformGroup("Common Collectables");
                return;
            }

            Container
                .BindMemoryPool<CollectableView, CommonCollectablePool>()
                .WithInitialSize(config.initialPoolSize)
                .ExpandByOneAtATime()
                .FromComponentInNewPrefab(config.commonCollectablePrefab)
                .UnderTransformGroup("Common Collectables");
        }
    }
}
