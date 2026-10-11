using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Systems.MineSystem.EnemySystem.Enum;
using Systems.MineSystem.EnemySystem.Model;
using Systems.MineSystem.Mine.Model;
using Systems.MineSystem.PauseSystem.Interface;

namespace Systems.MineSystem.EnemySystem.Interface
{
    public interface IEnemyController : IPausable, IDisposable
    {
        Guid EnemyId { get; }
        EnemyType EnemyType { get; }
        bool IsActive { get; }
        bool IsDead { get; }
        GridPosition CurrentGridPosition { get; }

        void Initialize(EnemyInitializeData initializeData);
        void OnFixedTick(EnemyTickContext tickContext);
        UniTask SpawnAsync(CancellationToken cancellationToken);
        UniTask DespawnAsync(CancellationToken cancellationToken);
        void Release();
    }
}
