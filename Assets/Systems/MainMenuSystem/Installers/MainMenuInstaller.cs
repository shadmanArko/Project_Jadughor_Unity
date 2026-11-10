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

        [Header("New Game Setup (Godot NewGameSetupUi)")]
        [Tooltip("Optional hand-made prefab with a NewGameSetupView (all references assigned). " +
                 "Leave empty to build the Godot layout at runtime.")]
        [SerializeField] private NewGameSetupView newGameSetupPrefab;
        [Tooltip("Godot's male_and_female_mainj.png placeholder shown on the right.")]
        [SerializeField] private Sprite characterArt;
        [Tooltip("Godot shipped the Show Tutorial switch disabled (always ON). Tick to let players turn tutorials off.")]
        [SerializeField] private bool tutorialToggleInteractable = true;
        [SerializeField] private string museumSceneName = "Museum";

        public override void InstallBindings()
        {
            Container.Bind<MainMenuView>().FromComponentInNewPrefab(mainMenuView).AsSingle();

            Container.Bind<NewGameSetupView>().FromMethod(_ =>
            {
                if (newGameSetupPrefab != null)
                    return Container.InstantiatePrefabForComponent<NewGameSetupView>(newGameSetupPrefab);
                return NewGameSetupBuilder.Build(characterArt, tutorialToggleInteractable);
            }).AsSingle();

            Container.BindInstance(museumSceneName).WithId("MuseumSceneName");

            Container.BindInterfacesAndSelfTo<MainMenuModel>()
                .AsSingle();

            Container.BindInterfacesAndSelfTo<NewGameSetupController>()
                .AsSingle();

            Container.BindInterfacesAndSelfTo<MainMenuController>()
                .AsSingle()
                .NonLazy();
        }
    }
}
