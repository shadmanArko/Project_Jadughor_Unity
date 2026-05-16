using Core.EventBus;
using UnityEngine;
using Zenject;

namespace Core.Installer
{
    /// <summary>
    /// Zenject installer for global core infrastructure.
    /// Must be added to the ProjectContext asset so bindings are available
    /// across all scenes.
    ///
    /// Setup:
    ///   1. Create a ScriptableObject asset: Assets/Installers/CoreInstaller.asset
    ///   2. Open the ProjectContext prefab (or create one via Zenject menu).
    ///   3. Add this CoreInstaller asset to ProjectContext → ScriptableObject Installers.
    /// </summary>
    [CreateAssetMenu(fileName = "CoreInstaller", menuName = "Installers/CoreInstaller")]
    public sealed class CoreInstaller : ScriptableObjectInstaller<CoreInstaller>
    {
        public override void InstallBindings()
        {
            // EventBus is a singleton for the entire application lifetime.
            // All systems receive it via constructor injection.
            Container.Bind<EventBus.EventBus>().AsSingle().NonLazy();
        }
    }
}
