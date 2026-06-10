using Systems.MineSystem.MinePlayerSystem.Config;
using Systems.MineSystem.MinePlayerSystem.Controller;
using Systems.MineSystem.MinePlayerSystem.Model;
using Systems.MineSystem.MinePlayerSystem.Scriptable;
using Systems.MineSystem.MinePlayerSystem.Service;
using Systems.MineSystem.MinePlayerSystem.View;
using UnityEngine;
using Zenject;

namespace Systems.MineSystem.MinePlayerSystem.Installer
{
    [CreateAssetMenu(fileName = "MinePlayerInstaller", menuName = "Installers/MinePlayerInstaller")]
    public class MinePlayerInstaller : ScriptableObjectInstaller<MinePlayerInstaller>
    {
        [SerializeField] private MinePlayerDataConfig playerDataConfig;
        [SerializeField] private MinePlayerScriptable playerScriptable;
        [SerializeField] private PlayerView playerPrefab;
        
        public override void InstallBindings()
        {
            Container.Bind<MinePlayerDataConfig>()
                .FromScriptableObject(playerDataConfig).AsSingle();
            
            Container.Bind<MinePlayerScriptable>()
                .FromScriptableObject(playerScriptable).AsSingle();

            Container.Bind<RuntimeDataScriptable>()
                .FromInstance(CreateInstance<RuntimeDataScriptable>())
                .AsSingle();

            Container.Bind<InputSystem_Actions>()
                .AsSingle()
                .NonLazy();

            Container.BindInterfacesAndSelfTo<PlayerInputActionHandler>()
                .AsSingle()
                .NonLazy();

            if (playerPrefab == null)
                return;

            Container.Bind<PlayerView>()
                .FromComponentInNewPrefab(playerPrefab)
                .AsSingle()
                .NonLazy();
            Container.BindInterfacesAndSelfTo<PlayerMovementService>()
                .AsSingle();
            Container.BindInterfacesAndSelfTo<PlayerModel>().AsSingle();
            Container.BindInterfacesAndSelfTo<PlayerController>()
                .AsSingle()
                .NonLazy();
        }
    }
}
