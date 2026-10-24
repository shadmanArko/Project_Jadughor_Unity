using Systems.MineSystem.MineTransitionSystem.Config;
using Systems.MineSystem.MineTransitionSystem.Controller;
using Systems.MineSystem.MineTransitionSystem.Service;
using Systems.MineSystem.MineTransitionSystem.View;
using Systems.MineSystem.Utilities.Camera;
using UnityEngine;
using Zenject;

namespace Systems.MineSystem.MineTransitionSystem.Installer
{
    [CreateAssetMenu(fileName = "MineTransitionInstaller", menuName = "Installers/Mine Transition Installer")]
    public sealed class MineTransitionInstaller : ScriptableObjectInstaller<MineTransitionInstaller>
    {
        [Tooltip("Camp presentation prefab used by mine entry transitions.")]
        [SerializeField] private CampView campPrefab;
        [Tooltip("Popup canvas prefab for camp and mine transition choices.")]
        [SerializeField] private MineTransitionCanvasView canvasPrefab;
        [Tooltip("Authored trigger for the mine exit at the entrance cell.")]
        [SerializeField] private MineEntranceTriggerView entranceTriggerPrefab;
        [Tooltip("Authored movement and camera timing for mine transitions.")]
        [SerializeField] private MineTransitionConfig transitionConfig;
        [Tooltip("Authored Cinemachine settings for the mine camera.")]
        [SerializeField] private MineCameraConfig cameraConfig;

        public override void InstallBindings()
        {
            if (campPrefab == null || canvasPrefab == null ||
                entranceTriggerPrefab == null || transitionConfig == null ||
                cameraConfig == null)
                throw new System.InvalidOperationException(
                    "MineTransitionInstaller requires camp, popup, entrance trigger, transition config, and camera config assets.");
            Container.Bind<CampView>().FromComponentInNewPrefab(campPrefab)
                .AsSingle().NonLazy();
            Container.Bind<MineTransitionCanvasView>()
                .FromComponentInNewPrefab(canvasPrefab).AsSingle().NonLazy();
            Container.Bind<MineEntranceTriggerView>()
                .FromComponentInNewPrefab(entranceTriggerPrefab).AsSingle().NonLazy();
            Container.Bind<MineTransitionConfig>()
                .FromScriptableObject(transitionConfig).AsSingle();
            Container.Bind<MineCameraConfig>()
                .FromScriptableObject(cameraConfig).AsSingle();
            Container.BindInterfacesAndSelfTo<MineCameraController>()
                .AsSingle().NonLazy();
            Container.Bind<CampToMineService>().AsSingle();
            Container.BindInterfacesAndSelfTo<MineTransitionPauseService>()
                .AsSingle();
            Container.Bind<CampToMuseumService>().AsSingle();
            Container.Bind<MineToCampService>().AsSingle();
            Container.BindInterfacesAndSelfTo<MineTransitionController>()
                .AsSingle().NonLazy();
        }
    }
}
