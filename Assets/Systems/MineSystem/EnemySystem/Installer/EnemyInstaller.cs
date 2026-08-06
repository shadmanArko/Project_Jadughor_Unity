using Systems.MineSystem.EnemySystem.Controller;
using Systems.MineSystem.EnemySystem.Config;
using Systems.MineSystem.EnemySystem.Interface;
using Systems.MineSystem.EnemySystem.Mob.BlackBat.Config;
using Systems.MineSystem.EnemySystem.Mob.BlackBat.Controller;
using Systems.MineSystem.EnemySystem.Mob.BlackBat.Service;
using Systems.MineSystem.EnemySystem.Mob.GreenSlime.Config;
using Systems.MineSystem.EnemySystem.Mob.GreenSlime.Service;
using Systems.MineSystem.EnemySystem.Mob.RattleSnake.Config;
using Systems.MineSystem.EnemySystem.Mob.RattleSnake.Service;
using Systems.MineSystem.EnemySystem.Service;
using UnityEngine;
using Zenject;

namespace Systems.MineSystem.EnemySystem.Installer
{
    [CreateAssetMenu(fileName = "EnemyInstaller", menuName = "Installers/Enemy Installer")]
    public sealed class EnemyInstaller : ScriptableObjectInstaller<EnemyInstaller>
    {
        [SerializeField] private SlimeConfigScriptable greenSlimeConfig;
        [SerializeField] private BatConfigScriptable blackBatConfig;
        [SerializeField] private SnakeConfigScriptable rattleSnakeConfig;
        [SerializeField] private EnemyWaveConfig enemyWaveConfig;

        public override void InstallBindings()
        {
            if (greenSlimeConfig == null)
                throw new System.InvalidOperationException(
                    "EnemyInstaller requires GreenSlimeConfig.");
            Container.Bind<SlimeConfigScriptable>()
                .FromScriptableObject(greenSlimeConfig)
                .AsSingle();
            if (blackBatConfig == null)
                throw new System.InvalidOperationException(
                    "EnemyInstaller requires BlackBatConfig.");
            Container.Bind<BatConfigScriptable>()
                .FromScriptableObject(blackBatConfig)
                .AsSingle();
            if (rattleSnakeConfig == null)
                throw new System.InvalidOperationException(
                    "EnemyInstaller requires RattleSnakeConfig.");
            Container.Bind<SnakeConfigScriptable>()
                .FromScriptableObject(rattleSnakeConfig)
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
            Container.Bind<IEnemyPlacementValidator>()
                .To<EnemyPlacementValidator>().AsSingle();
            Container.Bind<IEnemyChaseTargetResolver>()
                .To<EnemyChaseTargetResolver>().AsSingle();
            Container.BindInterfacesAndSelfTo<EnemyPathfindingService>()
                .AsSingle();
            Container.Bind<EnemySpawnCandidateService>().AsSingle();
            Container.Bind<BatNavigationService>().AsSingle();
            Container.BindInterfacesAndSelfTo<BatFormationService>().AsSingle();

            Container.BindInterfacesAndSelfTo<SlimePool>().AsSingle();
            Container.Bind<IEnemyFactory>()
                .To<SlimeFactory>().AsSingle();
            Container.BindInterfacesAndSelfTo<BatPool>().AsSingle();
            Container.Bind<IEnemyFactory>()
                .To<BatFactory>().AsSingle();
            Container.BindInterfacesAndSelfTo<SnakePool>().AsSingle();
            Container.Bind<IEnemyFactory>()
                .To<SnakeFactory>().AsSingle();

            Container.Bind<EnemyFactoryRegistry>().AsSingle();
            Container.Bind<EnemySpawnLocator>().AsSingle();
            Container.Bind<EnemySpawnService>().AsSingle();
            Container.BindInterfacesAndSelfTo<EnemyWaveService>()
                .AsSingle()
                .NonLazy();
            Container.BindInterfacesAndSelfTo<EnemyManager>()
                .AsSingle()
                .NonLazy();
            Container.BindInterfacesAndSelfTo<BatCaveSpawnController>()
                .AsSingle()
                .NonLazy();
        }
    }
}
