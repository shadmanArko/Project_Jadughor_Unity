using System;
using Systems.MainMenuSystem.Controllers;
using Systems.MainMenuSystem.Models;
using Systems.MainMenuSystem.Views;
using UnityEngine;
using Zenject;

namespace Systems.MainMenuSystem.Installers
{
    [CreateAssetMenu(fileName = "MainMenuInstaller", menuName = "Installers/MainMenuInstaller")]
    public class MainMenuInstaller : ScriptableObjectInstaller<MainMenuInstaller>
    {
        [SerializeField] private MainMenuView mainMenuView;
        
        public override void InstallBindings()
        {
            Container.Bind<MainMenuView>().FromComponentInNewPrefab(mainMenuView).AsSingle();

            Container.BindInterfacesAndSelfTo<MainMenuModel>()
                .AsSingle();
            
            Container.BindInterfacesAndSelfTo<MainMenuController>()
                .AsSingle()
                .NonLazy();
        }
    }
}