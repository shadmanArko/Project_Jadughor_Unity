using Systems.MineSystem.EnemySystem.Interface;
using Systems.MineSystem.EnemySystem.Model;

namespace Systems.MineSystem.EnemySystem.Service
{
    public sealed class EnemySpawnService
    {
        private readonly EnemyFactoryRegistry _factories;
        private readonly EnemySpawnLocator _locator;

        public EnemySpawnService(
            EnemyFactoryRegistry factories,
            EnemySpawnLocator locator)
        {
            _factories = factories;
            _locator = locator;
        }

        public EnemySpawnResult Spawn(EnemySpawnRequest request)
        {
            if (request.Config == null)
                return EnemySpawnResult.Failure("Enemy spawn config is missing.");
            if (!request.Config.Validate(out var validationError))
                return EnemySpawnResult.Failure(validationError);
            if (!_factories.TryGet(request.Config.EnemyType, out var factory))
            {
                return EnemySpawnResult.Failure(
                    $"No factory is registered for {request.Config.EnemyType}.");
            }
            if (!_locator.TryLocate(request, out var spawnData, out var locatorError))
                return EnemySpawnResult.Failure(locatorError);

            var enemy = factory.Create(request, spawnData);
            return enemy == null
                ? EnemySpawnResult.Failure("Enemy factory returned no controller.")
                : EnemySpawnResult.Success(enemy);
        }

        public void Release(IEnemyController enemy)
        {
            if (enemy != null && _factories.TryGet(enemy.EnemyType, out var factory))
                factory.Release(enemy);
        }
    }
}
