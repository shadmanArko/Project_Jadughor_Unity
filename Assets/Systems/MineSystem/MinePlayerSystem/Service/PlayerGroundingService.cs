using Systems.MineSystem.MinePlayerSystem.Config;
using Systems.MineSystem.MinePlayerSystem.Scriptable;
using Systems.MineSystem.MinePlayerSystem.View;
using UnityEngine;

namespace Systems.MineSystem.MinePlayerSystem.Service
{
    public sealed class PlayerGroundingService : IPlayerFixedTickService
    {
        private readonly PlayerView _view;
        private readonly RuntimeDataScriptable _runtime;
        private readonly MinePlayerDataConfig _config;
        private readonly RaycastHit2D[] _hits = new RaycastHit2D[8];

        public PlayerGroundingService(
            PlayerView view,
            RuntimeDataScriptable runtime,
            MinePlayerDataConfig config)
        {
            _view = view;
            _runtime = runtime;
            _config = config;
        }

        public void OnFixedTick()
        {
            var bounds = _view.GroundCollider.bounds;
            var width = Mathf.Max(
                0.001f,
                bounds.size.x - _config.groundProbeWidthInset * 2f);
            var origin = new Vector2(bounds.center.x, bounds.min.y);
            var size = new Vector2(
                width,
                _config.groundProbeThickness);
            var filter = new ContactFilter2D
            {
                useLayerMask = true,
                layerMask = _config.wallLayerMask,
                useTriggers = false
            };

            var hitCount = Physics2D.BoxCast(
                origin,
                size,
                0f,
                Vector2.down,
                filter,
                _hits,
                _config.groundProbeDistance);

            _runtime.isGrounded.Value = false;
            _runtime.groundCollider = null;
            _runtime.groundNormal = Vector2.zero;

            for (var i = 0; i < hitCount; i++)
            {
                var hit = _hits[i];
                if (hit.collider == null ||
                    hit.normal.y < _config.minimumGroundNormalY)
                    continue;

                _runtime.isGrounded.Value = true;
                _runtime.groundCollider = hit.collider;
                _runtime.groundNormal = hit.normal;
                break;
            }
        }
    }
}
