using System.Threading;
using Cysharp.Threading.Tasks;
using Systems.MineSystem.MineTransitionSystem.Model;

namespace Systems.MineSystem.MineTransitionSystem.Service
{
    public sealed class CampToMuseumService
    {
        public UniTask<MineTransitionResult> ExecuteAsync(CancellationToken token) =>
            UniTask.FromResult(MineTransitionResult.Unavailable(
                "Camp-to-Museum transition is not configured yet."));
    }
}
