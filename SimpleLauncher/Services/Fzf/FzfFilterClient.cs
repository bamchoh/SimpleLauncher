using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using SimpleLauncher.Models;
using StreamJsonRpc;

namespace SimpleLauncher.Services.Fzf
{
    /// <summary>
    /// fzf-filterd デーモンを起動し、名前付きパイプ経由の JSON-RPC であいまい検索を行う。
    /// 接続は最初の呼び出し時に遅延して行われる。
    /// </summary>
    public sealed class FzfFilterClient : IFilterService, IDisposable
    {
        public const string DefaultPipeName = "winiotestpipe";
        public const string DaemonFileName = "fzf-filterd.exe";

        private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(10);

        private readonly string _pipeName;
        private readonly string _daemonPath;
        private readonly Lazy<Task<JsonRpc>> _rpc;

        private Process? _daemon;
        private NamedPipeClientStream? _stream;
        private bool _disposed;

        public FzfFilterClient()
            : this(Path.Combine(AppContext.BaseDirectory, DaemonFileName), DefaultPipeName)
        {
        }

        public FzfFilterClient(string daemonPath, string pipeName)
        {
            _daemonPath = daemonPath;
            _pipeName = pipeName;
            _rpc = new Lazy<Task<JsonRpc>>(ConnectAsync);
        }

        public async Task SetListAsync(IReadOnlyList<string> items)
        {
            var rpc = await _rpc.Value.ConfigureAwait(false);
            await rpc.InvokeAsync<SetListReply>("TestRPC.SetList", new SetListArgs { List = items.ToList() })
                .ConfigureAwait(false);
        }

        public async Task SetCommandListAsync(IReadOnlyList<string> items)
        {
            var rpc = await _rpc.Value.ConfigureAwait(false);
            await rpc.InvokeAsync<SetListReply>("TestRPC.SetCommandList", new SetListArgs { List = items.ToList() })
                .ConfigureAwait(false);
        }

        public async Task<IReadOnlyList<FilterResult>> FilterAsync(string pattern)
        {
            var rpc = await _rpc.Value.ConfigureAwait(false);
            var reply = await rpc.InvokeAsync<FilterReply>("TestRPC.Filter", new FilterArgs { Pattern = pattern })
                .ConfigureAwait(false);

            return reply?.Results ?? new List<FilterResult>();
        }

        private async Task<JsonRpc> ConnectAsync()
        {
            StartDaemon();

            var stream = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            try
            {
                await stream.ConnectAsync((int)ConnectTimeout.TotalMilliseconds).ConfigureAwait(false);
            }
            catch (TimeoutException ex)
            {
                stream.Dispose();
                throw new InvalidOperationException(
                    $"フィルタデーモン ({DaemonFileName}) に接続できませんでした。", ex);
            }

            _stream = stream;
            return JsonRpc.Attach(stream);
        }

        private void StartDaemon()
        {
            var psi = new ProcessStartInfo
            {
                FileName = File.Exists(_daemonPath) ? _daemonPath : Path.GetFileNameWithoutExtension(DaemonFileName),
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            try
            {
                _daemon = Process.Start(psi);
            }
            catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
            {
                // 既に起動済みのデーモンがいる可能性があるので接続は試みる
                Debug.WriteLine($"Failed to start {DaemonFileName}: {ex.Message}");
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            if (_rpc.IsValueCreated && _rpc.Value.IsCompletedSuccessfully)
            {
                _rpc.Value.Result.Dispose();
            }

            _stream?.Dispose();

            try
            {
                if (_daemon is { HasExited: false })
                {
                    _daemon.Kill();
                }
            }
            catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                Debug.WriteLine($"Failed to stop {DaemonFileName}: {ex.Message}");
            }

            _daemon?.Dispose();
        }
    }
}
