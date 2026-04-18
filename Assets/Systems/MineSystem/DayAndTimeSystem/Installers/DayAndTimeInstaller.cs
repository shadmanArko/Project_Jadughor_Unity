using Systems.MineSystem.DayAndTimeSystem.Configs;
using Systems.MineSystem.DayAndTimeSystem.Controllers;
using Systems.MineSystem.DayAndTimeSystem.Models;
using Systems.MineSystem.DayAndTimeSystem.Views;
using UnityEngine;
using Zenject;

namespace Systems.MineSystem.DayAndTimeSystem.Installers
{
    [CreateAssetMenu(fileName = "DayAndTimeInstaller", menuName = "Installers/DayAndTimeInstaller")]
    public class DayAndTimeInstaller : ScriptableObjectInstaller<DayAndTimeInstaller>
    {
        [SerializeField] private DayAndTimeView   dayAndTimeView;
        [SerializeField] private DayAndTimeConfig config;
        
        public override void InstallBindings()
        {
            Container.Bind<DayAndTimeConfig>()
                .FromScriptableObject(config)
                .AsSingle();

            Container.BindInterfacesAndSelfTo<DayAndTimeModel>()
                .AsSingle();

            Container.Bind<DayAndTimeView>()
                .FromComponentInNewPrefab(dayAndTimeView)
                .AsSingle();

            Container.BindInterfacesAndSelfTo<DayAndTimeController>()
                .AsSingle()
                .NonLazy();
        }
    }
}