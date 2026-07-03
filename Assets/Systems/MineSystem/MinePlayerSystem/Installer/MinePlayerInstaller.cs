using Systems.MineSystem.MinePlayerSystem.Config;
using Systems.MineSystem.MinePlayerSystem.Controller;
using Systems.MineSystem.MinePlayerSystem.Model;
using Systems.MineSystem.MinePlayerSystem.Scriptable;
using Systems.MineSystem.MinePlayerSystem.Service;
using Systems.MineSystem.MinePlayerSystem.SubSystem.PlayerAnimationSubSystem.Model;
using Systems.MineSystem.MinePlayerSystem.SubSystem.PlayerAnimationSubSystem.Scriptable;
using Systems.MineSystem.MinePlayerSystem.SubSystem.PlayerAnimationSubSystem.Service;
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
        [SerializeField]
        private PlayerAnimationLibraryScriptable playerAnimationLibrary;
        [SerializeField] private string animationProfileId;
        
        public override void InstallBindings()
        {
            Container.Bind<MinePlayerDataConfig>()
                .FromScriptableObject(playerDataConfig).AsSingle();
            
            Container.Bind<MinePlayerScriptable>()
                .FromScriptableObject(playerScriptable).AsSingle();

            Container.Bind<RuntimeDataScriptable>()
                .FromInstance(CreateInstance<RuntimeDataScriptable>())
                .AsSingle();

            Container.Bind<PlayerPauseStateData>().AsSingle();

            Container.Bind<InputSystem_Actions>()
                .AsSingle()
                .NonLazy();

            Container.BindInterfacesAndSelfTo<PlayerInputActionHandler>()
                .AsSingle()
                .NonLazy();

            // Input can exist before a playable character prefab is configured.
            if (playerPrefab == null ||
                playerAnimationLibrary == null ||
                !playerAnimationLibrary.TryGetProfile(
                    animationProfileId,
                    out var animationProfile))
                return;

            Container.Bind<PlayerAnimationLibraryScriptable>()
                .FromScriptableObject(playerAnimationLibrary)
                .AsSingle();
            Container.Bind<AnimationProfile>()
                .FromInstance(animationProfile)
                .AsSingle();
            Container.Bind<PlayerView>()
                .FromComponentInNewPrefab(playerPrefab)
                .AsSingle()
                .NonLazy();

            Container.Bind<PlayerGroundingService>().AsSingle();
            Container.Bind<PlayerFallService>().AsSingle();
            Container.Bind<PlayerDeathService>().AsSingle();
            Container.BindInterfacesAndSelfTo<PlayerDamageService>().AsSingle();
            Container.Bind<PlayerClimbService>().AsSingle();
            Container.BindInterfacesAndSelfTo<PlayerActionService>().AsSingle();
            Container.BindInterfacesAndSelfTo<PlayerInteractionService>()
                .AsSingle()
                .NonLazy();
            Container.Bind<PlayerMovementService>().AsSingle();
            Container.BindInterfacesAndSelfTo<PlayerAutoMovementService>().AsSingle();
            Container.Bind<PlayerAutoAnimationService>().AsSingle();
            Container.BindInterfacesAndSelfTo<PlayerAnimationService>()
                .AsSingle()
                .NonLazy();
            Container.BindInterfacesAndSelfTo<PlayerModel>().AsSingle();
            Container.Bind<PlayerTransitionService>().AsSingle();
            Container.BindInterfacesAndSelfTo<PlayerController>()
                .AsSingle()
                .NonLazy();
        }
    }
}
