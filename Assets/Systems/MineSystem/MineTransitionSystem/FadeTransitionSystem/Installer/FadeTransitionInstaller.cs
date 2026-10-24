using System;
using Systems.MineSystem.MineTransitionSystem.FadeTransitionSystem.Config;
using Systems.MineSystem.MineTransitionSystem.FadeTransitionSystem.Controller;
using Systems.MineSystem.MineTransitionSystem.FadeTransitionSystem.Model;
using Systems.MineSystem.MineTransitionSystem.FadeTransitionSystem.View;
using UnityEngine;
using Zenject;

namespace Systems.MineSystem.MineTransitionSystem.FadeTransitionSystem.Installer
{
    [CreateAssetMenu(fileName = "FadeTransitionInstaller", menuName = "Installers/Mine Fade Transition")]
    public sealed class FadeTransitionInstaller : ScriptableObjectInstaller<FadeTransitionInstaller>
    {
        [Tooltip("Authored full-screen fade canvas prefab.")]
        [SerializeField] private FadeTransitionCanvasView canvasPrefab;
        [Tooltip("Timing for the overnight fade and day card.")]
        [SerializeField] private FadeTransitionConfig config;

        public override void InstallBindings()
        {
            if (canvasPrefab == null || config == null)
                throw new InvalidOperationException(
                    "FadeTransitionInstaller requires a canvas prefab and config.");

            Container.Bind<FadeTransitionConfig>().FromScriptableObject(config).AsSingle();
            Container.Bind<FadeTransitionCanvasView>()
                .FromComponentInNewPrefab(canvasPrefab).AsSingle().NonLazy();
            Container.BindInterfacesAndSelfTo<FadeTransitionModel>().AsSingle();
            Container.BindInterfacesAndSelfTo<FadeTransitionController>()
                .AsSingle().NonLazy();
        }
    }
}
