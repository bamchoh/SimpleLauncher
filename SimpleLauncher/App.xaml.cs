using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using SimpleLauncher.Config;
using SimpleLauncher.Interop;
using SimpleLauncher.Services;
using SimpleLauncher.ViewModels;
using SimpleLauncher.Views;

namespace SimpleLauncher
{
    /// <summary>
    /// アプリケーションのエントリポイント兼コンポジションルート。
    /// 多重起動を抑止し、タスクトレイ常駐のメインウィンドウを組み立てる。
    /// </summary>
    public partial class App : System.Windows.Application
    {
        private const string FallbackMutexGuid = "00000000-1111-2222-3333-444444444444";

        private Mutex? _mutex;
        private bool _ownsMutex;
        private MainWindow? _mainWindow;
        private System.Windows.Forms.NotifyIcon? _notifyIcon;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            if (!TryAcquireSingleInstanceMutex())
            {
                Shutdown();
                return;
            }

            try
            {
                _mainWindow = CreateMainWindow();
                _mainWindow.ShowInTaskbar = false;
                _mainWindow.Hide();

                _notifyIcon = CreateNotifyIcon();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _notifyIcon?.Dispose();

            if (_mutex != null)
            {
                if (_ownsMutex)
                {
                    _mutex.ReleaseMutex();
                }

                _mutex.Dispose();
            }

            base.OnExit(e);
        }

        private MainWindow CreateMainWindow()
        {
            var settings = AppSettings.Load(AppContext.BaseDirectory);
            var hotKey = HotKeyData.Parse(settings.HotKey);

            var shortcutResolver = new ShellLinkResolver();

            var viewModel = new MainWindowVM(
                new LocalFilterService(),
                new InstalledAppScanner(shortcutResolver),
                new AppDataConfigStore(),
                new RecentFilesScanner(shortcutResolver),
                new ProcessLauncher(),
                new LaunchPlanner(shortcutResolver),
                new MessageBoxErrorReporter());

            var window = new MainWindow(viewModel, hotKey);

            // 一覧の読み込みはウィンドウ表示をブロックしない
            _ = viewModel.InitializeAsync();

            return window;
        }

        private System.Windows.Forms.NotifyIcon CreateNotifyIcon()
        {
            var iconStream = GetResourceStream(new Uri("favicon.ico", UriKind.Relative))?.Stream
                ?? throw new InvalidOperationException("favicon.ico が見つかりません。");

            var menu = new System.Windows.Forms.ContextMenuStrip();
            menu.Items.Add("設定", null, (_, _) => ShowMainWindow());
            menu.Items.Add("終了", null, (_, _) => Shutdown());

            return new System.Windows.Forms.NotifyIcon
            {
                Visible = true,
                Icon = new System.Drawing.Icon(iconStream),
                Text = "Simple Launcher",
                ContextMenuStrip = menu,
            };
        }

        private void ShowMainWindow()
        {
            _mainWindow?.Show();
            _mainWindow?.Activate();
        }

        private bool TryAcquireSingleInstanceMutex()
        {
            var guid = Assembly.GetExecutingAssembly().GetCustomAttribute<GuidAttribute>()?.Value ?? FallbackMutexGuid;
            var mutexName = $"{Process.GetCurrentProcess().ProcessName}-{guid}";

            _mutex = new Mutex(false, mutexName);

            try
            {
                _ownsMutex = _mutex.WaitOne(0);
            }
            catch (AbandonedMutexException)
            {
                // 前のインスタンスが解放せずに終了していた場合。所有権はこちらに移っている
                _ownsMutex = true;
            }

            if (!_ownsMutex)
            {
                _mutex.Dispose();
                _mutex = null;
            }

            return _ownsMutex;
        }

        private sealed class MessageBoxErrorReporter : IErrorReporter
        {
            public void ShowError(string message)
            {
                MessageBox.Show(message, "実行エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
