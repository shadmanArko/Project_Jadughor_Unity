using Systems.MineSystem.ToolbarSystem.Controller;
using Systems.MineSystem.ToolbarSystem.Handler;
using Systems.MineSystem.ToolbarSystem.Interface;
using Systems.MineSystem.ToolbarSystem.Model;
using Systems.MineSystem.ToolbarSystem.Profile;
using Systems.MineSystem.ToolbarSystem.Scriptable;
using Systems.MineSystem.ToolbarSystem.Service;
using Systems.MineSystem.ToolbarSystem.View;
using Systems.MineSystem.CollectableSystem.Interface;
using UnityEngine;
using Zenject;

namespace Systems.MineSystem.ToolbarSystem.Installer
{
    [CreateAssetMenu(fileName = "ToolbarInstaller", menuName = "Installers/Toolbar Installer")]
    public sealed class ToolbarInstaller : ScriptableObjectInstaller<ToolbarInstaller>
    {
        [SerializeField] private ToolbarConfig config;
        [SerializeField] private ItemActionProfileCatalog itemActionProfiles;
        [SerializeField] private PlaceableFactoryCatalog placeableFactories;

        public override void InstallBindings()
        {
            Container.Bind<ToolbarConfig>()
                .FromScriptableObject(config)
                .AsSingle();

            Container.Bind<ToolbarCanvasView>()
                .FromComponentInNewPrefab(config.ToolbarCanvasPrefab)
                .AsSingle()
                .NonLazy();
            Container.Bind<IToolbarView>()
                .To<ToolbarCanvasView>()
                .FromResolve();

            Container.Bind<ToolbarModel>().AsSingle();
            Container.Bind<IToolbarInventorySource>()
                .To<ToolbarInventorySource>()
                .AsSingle();
            Container.BindInterfacesAndSelfTo<ToolbarInputService>()
                .AsSingle();
            Container.BindInterfacesAndSelfTo<ToolbarController>()
                .AsSingle()
                .NonLazy();

            Container.Bind<ItemActionProfileCatalog>()
                .FromScriptableObject(itemActionProfiles)
                .AsSingle();
            Container.Bind<PlaceableFactoryCatalog>()
                .FromScriptableObject(placeableFactories)
                .AsSingle();
            Container.Bind<ICollectableSpriteProvider>()
                .To<ProfileItemSpriteProvider>()
                .AsSingle();

            Container.BindInterfacesAndSelfTo<ItemTargetResolver>()
                .AsSingle();
            Container.Bind<IPlaceableValidator>()
                .To<PlaceableValidator>()
                .AsSingle();
            Container.BindInterfacesAndSelfTo<PlaceableFactory>()
                .AsSingle();

            Container.Bind<IItemActionHandler>()
                .To<ToolItemActionHandler>()
                .AsSingle();
            Container.Bind<IItemActionHandler>()
                .To<WeaponItemActionHandler>()
                .AsSingle();
            Container.Bind<IItemActionHandler>()
                .To<ConsumableItemActionHandler>()
                .AsSingle();
            Container.Bind<IItemActionHandler>()
                .To<PlaceableItemActionHandler>()
                .AsSingle();

            Container.BindInterfacesAndSelfTo<ItemActionController>()
                .AsSingle()
                .NonLazy();
        }
    }
}
