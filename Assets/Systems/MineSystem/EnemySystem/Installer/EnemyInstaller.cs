using Systems.MineSystem.EnemySystem.Controller;
using Systems.MineSystem.EnemySystem.Config;
using Systems.MineSystem.EnemySystem.Interface;
using Systems.MineSystem.EnemySystem.Mob.Slime.Config;
using Systems.MineSystem.EnemySystem.Mob.Slime.Service;
using Systems.MineSystem.EnemySystem.Service;
using UnityEngine;
using Zenject;

namespace Systems.MineSystem.EnemySystem.Installer
{
    [CreateAssetMenu(fileName = "EnemyInstaller", menuName = "Installers/Enemy Installer")]
    public sealed class EnemyInstaller : ScriptableObjectInstaller<EnemyInstaller>
    {
        [SerializeField] private SlimeConfigScriptable greenSlimeConfig;
        [SerializeField] private EnemyWaveConfig enemyWaveConfig;

        public override void InstallBindings()
        {
            if (greenSlimeConfig == null)
                throw new System.InvalidOperationException(
                    "EnemyInstaller requires GreenSlimeConfig.");
            Container.Bind<SlimeConfigScriptable>()
                .FromScriptableObject(greenSlimeConfig)
                .AsSingle();
            if (enemyWaveConfig == null)
                throw new System.InvalidOperationException(
                    "EnemyInstaller requires EnemyWaveConfig.");
            Container.Bind<EnemyWaveConfig>()
                .FromScriptableObject(enemyWaveConfig)
                .AsSingle();

            Container.Bind<IEnemyTargetProvider>()
                .To<EnemyTargetProvider>().AsSingle();
            Container.Bind<IEnemyStatusEffectApplier>()
                .To<NoOpEnemyStatusEffectApplier>().AsSingle();
            Container.Bind<IEnemyAttackService>()
                .To<EnemyAttackService>().AsSingle();
            Container.BindInterfacesAndSelfTo<EnemyPathfindingService>()
                .AsSingle();

            Container.Bind<IEnemySpawnRule>()
                .To<SlimeSpawnRule>().AsSingle();
            Container.BindInterfacesAndSelfTo<SlimePool>().AsSingle();
            Container.Bind<IEnemyFactory>()
                .To<SlimeFactory>().AsSingle();

            Container.Bind<EnemyFactoryRegistry>().AsSingle();
            Container.Bind<EnemySpawnLocator>().AsSingle();
            Container.Bind<EnemySpawnService>().AsSingle();
            Container.BindInterfacesAndSelfTo<EnemyWaveService>()
                .AsSingle()
                .NonLazy();
            Container.BindInterfacesAndSelfTo<EnemyManager>()
                .AsSingle()
                .NonLazy();
        }
    }
}
