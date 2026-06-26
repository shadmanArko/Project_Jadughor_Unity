using System.Threading;
using Cysharp.Threading.Tasks;
using Systems.MineSystem.Mine.Service.VisualizerService;
using Systems.MineSystem.ToolbarSystem.Model;
using UnityEngine;

namespace Systems.MineSystem.Mine.Service.Stalactite.Script
{
    public sealed class StalactiteRuntime : CaveFormationRuntime
    {
        private Rigidbody2D _body;
        private bool _isFalling;
        private bool _hasImpacted;

        public override void Initialize(PlaceableSpawnContext context)
        {
            base.Initialize(context);
            ConfigureBody();
            _isFalling = false;
            _hasImpacted = false;
        }

        public override void HandleTriggerEnter(Collider2D other)
        {
            if (!_isFalling ||
                _hasImpacted ||
                other == null ||
                other.transform.IsChildOf(transform))
                return;

            if (TryDamageTarget(other, Config.stalactiteDamage) ||
                IsWallCollider(other))
                _hasImpacted = true;
        }

        protected override async UniTask BreakAsync(
            CancellationToken cancellationToken)
        {
            if (!TryBeginBreak())
                return;

            _isFalling = false;
            _hasImpacted = false;

            await PlayStateAsync(
                Config.collapseState,
                Config.collapseDuration,
                cancellationToken);
            await FallAsync(cancellationToken);
            await PlayStateAsync(
                Config.shatterState,
                Config.shatterDuration,
                cancellationToken);
            FinishBreak();
        }

        private async UniTask FallAsync(CancellationToken cancellationToken)
        {
            if (Animator != null &&
                !string.IsNullOrWhiteSpace(Config.fallState))
                Animator.Play(Config.fallState, 0, 0f);

            ConfigureBody();
            _isFalling = true;

            var startY = _body.position.y;

            try
            {
                while (!cancellationToken.IsCancellationRequested &&
                       !_hasImpacted &&
                       startY - _body.position.y <
                       Config.stalactiteMaxFallDistance)
                {
                    var next = _body.position +
                               Vector2.down *
                               (Config.stalactiteFallSpeed *
                                Time.fixedDeltaTime);
                    _body.MovePosition(next);
                    await UniTask.WaitForFixedUpdate(cancellationToken);
                }
            }
            finally
            {
                _isFalling = false;
            }
        }

        private void ConfigureBody()
        {
            if (_body == null)
                _body = GetComponent<Rigidbody2D>();
            if (_body == null)
                _body = gameObject.AddComponent<Rigidbody2D>();

            _body.bodyType = RigidbodyType2D.Kinematic;
            _body.gravityScale = 0f;
            _body.constraints = RigidbodyConstraints2D.FreezeRotation;
            _body.useFullKinematicContacts = true;
            _body.simulated = true;
        }

        private bool IsWallCollider(Collider2D other)
        {
            return MineView != null &&
                   MineView.wallTileMap != null &&
                   other.transform == MineView.wallTileMap.transform;
        }
    }
}
