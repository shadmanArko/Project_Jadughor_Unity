using System;
using Cysharp.Threading.Tasks;

namespace Systems.MineSystem.PauseSystem.Service
{
    public sealed class PauseGate : IDisposable
    {
        private UniTaskCompletionSource _resumeSource;
        private bool _isPaused;
        private bool _disposed;

        public bool IsPaused => _isPaused;

        public void Pause()
        {
            if (_disposed || _isPaused) return;
            _isPaused = true;
            _resumeSource = new UniTaskCompletionSource();
        }

        public void Resume()
        {
            if (!_isPaused) return;
            _isPaused = false;
            _resumeSource?.TrySetResult();
            _resumeSource = null;
        }

        public UniTask WaitAsync()
        {
            if (!_isPaused) return UniTask.CompletedTask;
            _resumeSource ??= new UniTaskCompletionSource();
            return _resumeSource.Task;
        }

        public void Dispose()
        {
            if (_disposed) return;
            Resume();
            _disposed = true;
        }
    }
}
