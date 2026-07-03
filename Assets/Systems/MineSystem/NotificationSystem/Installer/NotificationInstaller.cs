using Systems.MineSystem.NotificationSystem.Config;
using Systems.MineSystem.NotificationSystem.Controller;
using Systems.MineSystem.NotificationSystem.View;
using UnityEngine;
using Zenject;

namespace Systems.MineSystem.NotificationSystem.Installer
{
    /// <summary>Installs the mine notification UI and controller.</summary>
    [CreateAssetMenu(fileName = "NotificationInstaller",
        menuName = "Installers/Notification Installer")]
    public sealed class NotificationInstaller :
        ScriptableObjectInstaller<NotificationInstaller>
    {
        [SerializeField] private NotificationCanvasView canvasPrefab;
        [SerializeField] private NotificationConfig config;

        public override void InstallBindings()
        {
            Container.Bind<NotificationConfig>()
                .FromScriptableObject(config).AsSingle();
            Container.Bind<NotificationCanvasView>()
                .FromComponentInNewPrefab(canvasPrefab)
                .AsSingle().NonLazy();
            Container.BindInterfacesAndSelfTo<NotificationController>()
                .AsSingle().NonLazy();
        }
    }
}
