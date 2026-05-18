using System;
using UnityEngine;
using Zenject;

namespace Systems.Utilities.Injector
{
    [Serializable]
    public class ManualInjector
    {
        private static DiContainer _container;

        public ManualInjector(DiContainer container)
        {
            _container = container;
        }

        public static void InjectDependencies(object target)
        {
            if (target == null)
            {
                Debug.LogError("Cannot inject into null target");
                return;
            }

            if (_container == null)
            {
                Debug.LogError("DiContainer is null");
                return;
            }

            _container.Inject(target);
        }
    }

}