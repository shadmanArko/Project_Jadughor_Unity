using Systems.MineSystem.BossLairSystem.Config;
using Systems.MineSystem.BossLairSystem.Controller;
using Systems.MineSystem.BossLairSystem.Handler;
using Systems.MineSystem.BossLairSystem.Model;
using Systems.MineSystem.BossLairSystem.Scriptable;
using Systems.MineSystem.BossLairSystem.Service;
using Systems.MineSystem.BossLairSystem.View;
using UnityEngine;
using Zenject;

namespace Systems.MineSystem.BossLairSystem.Installer
{
    /// <summary>
    /// Bindings for the boss lair feature. Add this to the Mine scene's
    /// SceneContext installer list.
    /// </summary>
    /// <remarks>
    /// The lair prefab is passed to <see cref="BossLairFactory"/> as a constructor
    /// argument rather than bound as a <see cref="BossLairView"/>. A
    /// <c>FromComponentInNewPrefab</c> binding would build the arena at startup on
    /// every run, including runs with no boss.
    /// <para>
    /// Arena geometry is not bound here: it lives on a
    /// <c>BossProceduralLairConfig</c> per boss, reached through the selected
    /// <c>BossProfileScriptable</c>.
    /// </para>
    /// </remarks>
    [CreateAssetMenu(
        fileName = "BossLairInstaller",
        menuName = "Installers/Boss Lair Installer")]
    public sealed class BossLairInstaller : ScriptableObjectInstaller<BossLairInstaller>
    {
        [Header("Configs")]
        [SerializeField] private BossLairConfig lairConfig;
        [SerializeField] private BossSpawnTableScriptable spawnTable;
        [SerializeField] private BossLairDecorScriptable decor;

        [Header("Prefabs")]
        [Tooltip("Arena prefab instantiated during mine generation when a gate is placed.")]
        [SerializeField] private BossLairView lairPrefab;

        public override void InstallBindings()
        {
            Container.Bind<BossLairConfig>()
                .FromScriptableObject(lairConfig).AsSingle();
            Container.Bind<BossSpawnTableScriptable>()
                .FromScriptableObject(spawnTable).AsSingle();
            Container.Bind<BossLairDecorScriptable>()
                .FromScriptableObject(decor).AsSingle();

            Container.BindInterfacesAndSelfTo<BossLairModel>().AsSingle();

            Container.Bind<BossLairPlacementService>().AsSingle();
            Container.Bind<BossSelectionService>().AsSingle();
            Container.Bind<BossGateSpawnService>().AsSingle();
            Container.Bind<BossLairShellGenerationService>().AsSingle();
            Container.Bind<BossLairDecorService>().AsSingle();
            Container.Bind<BossLairSpawnService>().AsSingle();
            Container.Bind<BossLairBuildService>().AsSingle();
            Container.Bind<BossLairEntryService>().AsSingle();
            Container.Bind<BossLairExitService>().AsSingle();

            Container.BindInterfacesAndSelfTo<BossGatePlacementService>()
                .AsSingle();
            Container.BindInterfacesAndSelfTo<BossLairCameraService>()
                .AsSingle();
            Container.BindInterfacesAndSelfTo<BossLairPauseService>()
                .AsSingle();
            Container.BindInterfacesAndSelfTo<BossLairFactory>()
                .AsSingle().WithArguments(lairPrefab);

            Container.BindInterfacesAndSelfTo<BossLairController>()
                .AsSingle().NonLazy();
            Container.BindInterfacesAndSelfTo<BossGateInteractionHandler>()
                .AsSingle();
        }
    }
}
