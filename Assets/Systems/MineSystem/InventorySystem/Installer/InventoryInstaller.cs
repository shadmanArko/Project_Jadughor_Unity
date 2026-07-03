using Systems.MineSystem.InventorySystem.Controller;
using Systems.MineSystem.InventorySystem.Interface;
using Systems.MineSystem.InventorySystem.Model;
using Systems.MineSystem.InventorySystem.Scriptable;
using Systems.MineSystem.InventorySystem.Service;
using Systems.MineSystem.InventorySystem.View;
using UnityEngine;
using Zenject;

namespace Systems.MineSystem.InventorySystem.Installer
{
    [CreateAssetMenu(
        fileName = "InventoryInstaller",
        menuName = "Installers/Inventory Installer")]
    public sealed class InventoryInstaller :
        ScriptableObjectInstaller<InventoryInstaller>
    {
        [SerializeField] private InventorySystemConfig config;

        public override void InstallBindings()
        {
            Container.Bind<InventorySystemConfig>()
                .FromScriptableObject(config)
                .AsSingle();
            Container.BindInterfacesAndSelfTo<InventoryModel>().AsSingle();
            Container.Bind<IInventoryService>()
                .To<InventoryService>()
                .AsSingle();
            Container.Bind<InventoryItemDescriptionService>().AsSingle();
            Container.Bind<InventoryCanvasView>()
                .FromComponentInNewPrefab(config.inventoryCanvasPrefab)
                .AsSingle()
                .NonLazy();
            Container.Bind<ItemCollectionVisualizerCanvasView>()
                .FromComponentInNewPrefab(
                    config.itemCollectionVisualizerCanvasPrefab)
                .AsSingle()
                .NonLazy();
            Container.BindInterfacesAndSelfTo<ItemCollectionVisualizerService>()
                .AsSingle()
                .NonLazy();
            Container.BindInterfacesAndSelfTo<InventoryController>()
                .AsSingle()
                .NonLazy();
        }
    }
}
