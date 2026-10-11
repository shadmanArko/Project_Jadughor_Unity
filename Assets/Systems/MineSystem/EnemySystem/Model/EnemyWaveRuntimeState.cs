namespace Systems.MineSystem.EnemySystem.Model
{
    public sealed class EnemyWaveRuntimeState
    {
        public EnemyWaveSpawnData Data { get; }
        public bool Triggered { get; set; }
        public int RemainingEnemyCount { get; set; }
        public int NextTriggerEvaluationMinute { get; set; }

        public EnemyWaveRuntimeState(EnemyWaveSpawnData data)
        {
            Data = data;
            RemainingEnemyCount = data.EnemyCount;
        }
    }
}
