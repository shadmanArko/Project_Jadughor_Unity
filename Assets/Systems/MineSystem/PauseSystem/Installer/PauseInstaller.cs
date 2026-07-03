using Systems.MineSystem.PauseSystem.Controller;
using Systems.MineSystem.PauseSystem.Model;
using UnityEngine;
using Zenject;

namespace Systems.MineSystem.PauseSystem.Installer
{
    [CreateAssetMenu(
        fileName = "PauseInstaller",
        menuName = "Installers/Pause Installer")]
    public sealed class PauseInstaller : ScriptableObjectInstaller<PauseInstaller>
    {
        public override void InstallBindings()
        {
            Container.BindInterfacesAndSelfTo<PauseModel>().AsSingle();
            Container.BindInterfacesAndSelfTo<PauseController>()
                .AsSingle()
                .NonLazy();
            Container.BindExecutionOrder<PauseController>(-10000);
            Container.BindInterfacesAndSelfTo<ScreenShakePauseController>()
                .AsSingle()
                .NonLazy();
        }
    }
}
