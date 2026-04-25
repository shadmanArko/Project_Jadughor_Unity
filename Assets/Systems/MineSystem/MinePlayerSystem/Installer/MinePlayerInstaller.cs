using Systems.MineSystem.MinePlayerSystem.Config;
using Systems.MineSystem.MinePlayerSystem.Scriptable;
using UnityEngine;
using Zenject;

namespace Systems.MineSystem.MinePlayerSystem.Installer
{
    [CreateAssetMenu(fileName = "MinePlayerInstaller", menuName = "Installers/MinePlayerInstaller")]
    public class MinePlayerInstaller : ScriptableObjectInstaller<MinePlayerInstaller>
    {
        [SerializeField] private MinePlayerDataConfig playerDataConfig;
        [SerializeField] private MinePlayerScriptable playerScriptable;
        
        public override void InstallBindings()
        {
            Container.Bind<MinePlayerDataConfig>()
                .FromScriptableObject(playerDataConfig).AsSingle();
            
            Container.Bind<MinePlayerScriptable>()
                .FromScriptableObject(playerScriptable).AsSingle();
        }
    }
}