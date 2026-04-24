using UnityEngine;
using Zenject;

namespace Systems.MineSystem.MineInstallers
{
    [CreateAssetMenu(fileName = "MineInstaller", menuName = "Installers/MineInstaller")]
    public class MineInstaller : ScriptableObjectInstaller<MineInstaller>
    {
        
        
        public override void InstallBindings()
        {
            
        }
    }
}