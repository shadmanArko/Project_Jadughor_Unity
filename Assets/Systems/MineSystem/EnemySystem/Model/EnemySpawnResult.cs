using Systems.MineSystem.EnemySystem.Interface;

namespace Systems.MineSystem.EnemySystem.Model
{
    public readonly struct EnemySpawnResult
    {
        public readonly bool Succeeded;
        public readonly IEnemyController Enemy;
        public readonly string Error;

        private EnemySpawnResult(
            bool succeeded,
            IEnemyController enemy,
            string error)
        {
            Succeeded = succeeded;
            Enemy = enemy;
            Error = error;
        }

        public static EnemySpawnResult Success(IEnemyController enemy) =>
            new(true, enemy, null);

        public static EnemySpawnResult Failure(string error) =>
            new(false, null, error);
    }
}
