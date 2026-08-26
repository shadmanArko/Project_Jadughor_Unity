using System;
using Systems.MineSystem.BossLairSystem.Model;
using Systems.MineSystem.BossLairSystem.View;
using Systems.MineSystem.Utilities.Camera;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Systems.MineSystem.BossLairSystem.Service
{
    /// <summary>
    /// Frames the boss arena, raising the camera zoom for the fight and
    /// restoring the mine's framing afterwards.
    /// </summary>
    /// <remarks>
    /// The authored orthographic size on the Cinemachine camera never runs:
    /// <see cref="PixelPerfectCamera"/> overwrites
    /// <see cref="UnityEngine.Camera.orthographicSize"/> every frame as
    /// <c>refResolutionY / (2 * assetsPPU)</c>. Window sizes here are therefore
    /// computed from that formula rather than read back from the camera, which
    /// would be a frame stale. Zoom is per boss, since the right value depends on
    /// arena size — a 15x8 arena frames well at 600, where the mine runs at 100.
    /// </remarks>
    public sealed class BossLairCameraService : IDisposable
    {
        private readonly UnityEngine.Camera _camera;
        private readonly MineCameraController _mineCamera;
        private PixelPerfectCamera _pixelPerfect;
        private int _mineAssetsPPU;
        private bool _isInLair;
        private bool _disposed;

        public BossLairCameraService(
            UnityEngine.Camera camera,
            MineCameraController mineCamera)
        {
            _camera = camera;
            _mineCamera = mineCamera;
        }

        private PixelPerfectCamera PixelPerfect =>
            _pixelPerfect ??= _camera.GetComponent<PixelPerfectCamera>();

        /// <summary>
        /// Visible world size for a given assets-per-unit value. Height comes
        /// from the PPU formula; width follows the <b>live</b> aspect ratio, so an
        /// ultrawide window is accounted for rather than assumed to be 16:9.
        /// </summary>
        public Vector2 ResolveWorldWindow(int assetsPPU)
        {
            var pixelPerfect = PixelPerfect;
            if (pixelPerfect == null || assetsPPU <= 0)
            {
                var fallbackHeight = _camera.orthographicSize * 2f;
                return new Vector2(fallbackHeight * _camera.aspect, fallbackHeight);
            }
            var height = pixelPerfect.refResolutionY / (float)assetsPPU;
            return new Vector2(height * _camera.aspect, height);
        }

        public float ResolveWindowHeightInCells(int assetsPPU, float cellWorldSize)
        {
            if (cellWorldSize <= 0f)
                return 0f;
            return ResolveWorldWindow(assetsPPU).y / cellWorldSize;
        }

        /// <summary>
        /// True when the arena is larger than the visible window in both axes, so
        /// the Cinemachine confiner has room to work. A confiner shrinks its
        /// bounding shape by the camera half-window, so a shape smaller than the
        /// window collapses and silently pins the camera, stopping player follow.
        /// </summary>
        public bool CanConfine(BossLairPlacement placement, int assetsPPU)
        {
            var window = ResolveWorldWindow(assetsPPU);
            var size = placement.InteriorWorldSize;
            return size.x > window.x && size.y > window.y;
        }

        public void EnterLair(
            BossLairView view,
            BossLairPlacement placement,
            int assetsPPU,
            Transform followTarget)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(BossLairCameraService));
            if (view == null)
                throw new ArgumentNullException(nameof(view));

            if (!_isInLair)
            {
                _mineAssetsPPU = ResolveMinePPU();
                _isInLair = true;
            }

            // The confiner must be off while its shape is swapped, then re-enabled
            // only if the arena is big enough to confine within.
            _mineCamera.ClearFollowTarget();
            _mineCamera.SetFreeMovement(true);
            ApplyAssetsPPU(assetsPPU);
            _mineCamera.SetBoundingShape(view.cameraBoundaryCollider);

            var centre = placement.InteriorWorldCenter;
            _mineCamera.SetPosition(
                new Vector3(centre.x, centre.y, _mineCamera.Position.z));

            if (CanConfine(placement, assetsPPU) && followTarget != null)
            {
                _mineCamera.SetFreeMovement(false);
                _mineCamera.SetFollowTarget(followTarget);
                return;
            }

            // Arena fits on screen: hold a fixed shot on its centre rather than
            // letting a degenerate confiner fight the follow target. The mine is
            // kept out of frame by the placement gap instead.
            _mineCamera.SetFreeMovement(true);
            _mineCamera.ClearFollowTarget();
        }

        public void ExitLair(Transform mineFollowTarget)
        {
            if (_disposed || !_isInLair)
                return;

            _mineCamera.ClearFollowTarget();
            _mineCamera.SetFreeMovement(true);
            ApplyAssetsPPU(_mineAssetsPPU);
            _mineCamera.RestoreMineBoundingShape();
            _mineCamera.SetFreeMovement(false);
            if (mineFollowTarget != null)
                _mineCamera.SetFollowTarget(mineFollowTarget);
            _isInLair = false;
        }

        private int ResolveMinePPU()
        {
            var pixelPerfect = PixelPerfect;
            return pixelPerfect != null ? pixelPerfect.assetsPPU : 0;
        }

        private void ApplyAssetsPPU(int assetsPPU)
        {
            var pixelPerfect = PixelPerfect;
            if (pixelPerfect == null || assetsPPU <= 0)
                return;
            pixelPerfect.assetsPPU = assetsPPU;
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            if (!_isInLair)
                return;
            try
            {
                // Restore the mine framing so a teardown mid-fight cannot leave a
                // live camera zoomed into a destroyed arena. Guarded because
                // during container teardown the camera may already be gone.
                _disposed = false;
                ExitLair(null);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
            finally
            {
                _disposed = true;
            }
        }
    }
}
