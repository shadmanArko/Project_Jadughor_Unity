using System;
using System.Collections.Generic;
using Systems.MineSystem.EnemySystem.Enum;
using Systems.MineSystem.EnemySystem.Interface;

namespace Systems.MineSystem.EnemySystem.Service
{
    public sealed class EnemyFactoryRegistry
    {
        private readonly Dictionary<EnemyType, IEnemyFactory> _factories = new();

        public EnemyFactoryRegistry(List<IEnemyFactory> factories)
        {
            for (var i = 0; i < factories.Count; i++)
            {
                var factory = factories[i];
                if (factory == null)
                    continue;
                if (_factories.ContainsKey(factory.EnemyType))
                {
                    throw new InvalidOperationException(
                        $"Multiple enemy factories are registered for {factory.EnemyType}.");
                }

                _factories.Add(factory.EnemyType, factory);
            }
        }

        public bool TryGet(EnemyType enemyType, out IEnemyFactory factory) =>
            _factories.TryGetValue(enemyType, out factory);
    }
}
