using System.Threading;
using Cysharp.Threading.Tasks;
using Systems.MineSystem.MineTransitionSystem.Model;

namespace Systems.MineSystem.MineTransitionSystem.Service
{
    public sealed class MineToCampService
    {
        public UniTask<MineTransitionResult> ExecuteAsync(CancellationToken token) =>
            UniTask.FromResult(MineTransitionResult.Unavailable(
                "Mine-to-Camp transition is not configured yet."));
    }
}
