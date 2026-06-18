using Systems.MineSystem.ToolbarSystem.Controller;
using Systems.MineSystem.ToolbarSystem.Interface;
using Systems.MineSystem.ToolbarSystem.Model;
using Systems.MineSystem.ToolbarSystem.Scriptable;
using Systems.MineSystem.ToolbarSystem.Service;
using Systems.MineSystem.ToolbarSystem.View;
using UnityEngine;
using Zenject;

namespace Systems.MineSystem.ToolbarSystem.Installer
{
    [CreateAssetMenu(fileName = "ToolbarInstaller", menuName = "Installers/Toolbar Installer")]
    public sealed class ToolbarInstaller : ScriptableObjectInstaller<ToolbarInstaller>
    {
        [SerializeField] private ToolbarConfig config;

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
        }
    }
}
