using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace SimpleLauncher.Interop
{
    /// <summary>
    /// グローバルホットキー (RegisterHotKey) の登録を管理する。
    /// WM_HOTKEY はスレッドのメッセージループから拾うので、ウィンドウが未表示でも動作する。
    /// </summary>
    public sealed class HotKeyHelper : IDisposable
    {
        private const int WmHotKey = 0x0312;
        private const int MaxHotKeyId = 0xC000;

        private readonly IntPtr _windowHandle;
        private readonly Dictionary<int, EventHandler> _handlers = new();
        private int _nextId;
        private bool _disposed;

        [DllImport("user32.dll")]
        private static extern int RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vk);

        [DllImport("user32.dll")]
        private static extern int UnregisterHotKey(IntPtr hWnd, int id);

        public HotKeyHelper(Window window)
        {
            _windowHandle = new WindowInteropHelper(window).Handle;
            ComponentDispatcher.ThreadPreprocessMessage += OnThreadPreprocessMessage;
        }

        /// <returns>登録できた場合 true。既に他のアプリが使っている等で失敗したら false。</returns>
        public bool Register(ModifierKeys modifiers, Key key, EventHandler handler)
        {
            var virtualKey = KeyInterop.VirtualKeyFromKey(key);

            while (_nextId < MaxHotKeyId)
            {
                var id = _nextId++;
                if (RegisterHotKey(_windowHandle, id, (int)modifiers, virtualKey) != 0)
                {
                    _handlers[id] = handler;
                    return true;
                }
            }

            return false;
        }

        public void UnregisterAll()
        {
            foreach (var id in _handlers.Keys)
            {
                UnregisterHotKey(_windowHandle, id);
            }

            _handlers.Clear();
        }

        private void OnThreadPreprocessMessage(ref MSG msg, ref bool handled)
        {
            if (msg.message != WmHotKey)
            {
                return;
            }

            if (_handlers.TryGetValue(msg.wParam.ToInt32(), out var handler))
            {
                handler(this, EventArgs.Empty);
                handled = true;
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            UnregisterAll();
            ComponentDispatcher.ThreadPreprocessMessage -= OnThreadPreprocessMessage;
        }
    }
}
