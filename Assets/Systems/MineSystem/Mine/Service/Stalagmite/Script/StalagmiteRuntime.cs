using System.Threading;
using Cysharp.Threading.Tasks;
using Systems.MineSystem.Mine.Service.VisualizerService;
using UnityEngine;

namespace Systems.MineSystem.Mine.Service.Stalagmite.Script
{
    public sealed class StalagmiteRuntime : CaveFormationRuntime
    {
        public override void HandleTriggerEnter(Collider2D other)
        {
            if (!TryDamageTarget(other, Config.stalagmiteContactDamage))
                return;

            BreakAsync(CancellationToken.None)
                .Forget(exception =>
                {
                    if (exception is not System.OperationCanceledException)
                        Debug.LogException(exception);
                });
        }

        protected override async UniTask BreakAsync(
            CancellationToken cancellationToken)
        {
            if (!TryBeginBreak())
                return;

            await PlayStateAsync(
                Config.shatterState,
                Config.shatterDuration,
                cancellationToken);
            FinishBreak();
        }
    }
}
