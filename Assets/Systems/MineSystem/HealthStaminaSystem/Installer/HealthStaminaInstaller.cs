using Systems.MineSystem.HealthStaminaSystem.Controller;
using Systems.MineSystem.HealthStaminaSystem.Model;
using Systems.MineSystem.HealthStaminaSystem.View;
using UnityEngine;
using Zenject;

namespace Systems.MineSystem.HealthStaminaSystem.Installer
{
    [CreateAssetMenu(fileName = "HealthStaminaInstaller", menuName = "Installers/HealthStaminaInstaller")]
    public class HealthStaminaInstaller : ScriptableObjectInstaller<HealthStaminaInstaller>
    {
        [SerializeField] private HealthStaminaCanvasView healthStaminaCanvasView;

        public override void InstallBindings()
        {
            Container.Bind<HealthStaminaCanvasView>().FromComponentInNewPrefab(healthStaminaCanvasView).AsSingle();
            Container.BindInterfacesAndSelfTo<HealthStaminaModel>().AsSingle();
            Container.BindInterfacesAndSelfTo<HealthStaminaController>().AsSingle().NonLazy();
        }
    }
}