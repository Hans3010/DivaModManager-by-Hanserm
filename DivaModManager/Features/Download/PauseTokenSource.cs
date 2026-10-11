using System.Threading;
using System.Threading.Tasks;

namespace DivaModManager.Features.Download
{
    /// <summary>
    /// ダウンロードの一時停止/再開を制御する
    /// </summary>
    public class PauseTokenSource
    {
        private readonly object _lock = new();
        private TaskCompletionSource<bool> _resumeTcs;

        public bool IsPaused
        {
            get { lock (_lock) { return _resumeTcs != null; } }
        }

        public PauseToken Token => new(this);

        public void Pause()
        {
            lock (_lock)
            {
                _resumeTcs ??= new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            }
        }

        public void Resume()
        {
            TaskCompletionSource<bool> tcs;
            lock (_lock)
            {
                tcs = _resumeTcs;
                _resumeTcs = null;
            }
            tcs?.TrySetResult(true);
        }

        internal Task WaitWhilePausedAsync(CancellationToken cancellationToken)
        {
            Task task;
            lock (_lock)
            {
                if (_resumeTcs == null) return Task.CompletedTask;
                task = _resumeTcs.Task;
            }
            return task.WaitAsync(cancellationToken);
        }
    }

    public readonly struct PauseToken
    {
        private readonly PauseTokenSource _source;

        public PauseToken(PauseTokenSource source)
        {
            _source = source;
        }

        public bool IsPaused => _source?.IsPaused ?? false;

        public Task WaitWhilePausedAsync(CancellationToken cancellationToken)
        {
            return _source == null ? Task.CompletedTask : _source.WaitWhilePausedAsync(cancellationToken);
        }
    }
}
