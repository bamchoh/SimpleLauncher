using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using SimpleLauncher.Interop;
using SimpleLauncher.ViewModels;

namespace SimpleLauncher.Views
{
    /// <summary>
    /// ランチャーのメインウィンドウ。キー操作を <see cref="MainWindowVM"/> の操作に変換する。
    /// 閉じる操作は非表示に置き換え、グローバルホットキーで再表示する。
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly MainWindowVM _viewModel;
        private readonly HotKeyHelper _hotKeyHelper;

        public MainWindow(MainWindowVM viewModel, HotKeyData hotKey)
        {
            InitializeComponent();

            _viewModel = viewModel;
            DataContext = viewModel;

            Loaded += (_, _) => FocusActivePatternBox(selectAll: false);

            _hotKeyHelper = new HotKeyHelper(this);
            if (!_hotKeyHelper.Register(hotKey.ModifierKeys, hotKey.Key, HotKeyPressed))
            {
                MessageBox.Show(
                    $"ホットキー {hotKey} を登録できませんでした。他のアプリが使用している可能性があります。",
                    "Simple Launcher",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        private TextBox ActivePatternTextBox => _viewModel.IsAppsTabActive ? PatternTextBox : RecentPatternTextBox;

        private ListBox ActiveListBox => _viewModel.IsAppsTabActive ? FilteredResultList : RecentFilesList;

        private void HotKeyPressed(object? sender, EventArgs e)
        {
            Show();
            Activate();
            FocusActivePatternBox(selectAll: true);
        }

        private void FocusActivePatternBox(bool selectAll)
        {
            // タブ切替直後はコンテンツが生成されていないことがあるので、描画後にフォーカスする
            Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
            {
                var textBox = ActivePatternTextBox;
                textBox.Focus();
                if (selectAll)
                {
                    textBox.SelectAll();
                }
            });
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            e.Handled = Keyboard.Modifiers == ModifierKeys.Control
                ? HandleControlKey(e.Key)
                : HandlePlainKey(e.Key);
        }

        private bool HandleControlKey(Key key)
        {
            switch (key)
            {
                case Key.N:
                    MoveSelection(+1);
                    return true;

                case Key.P:
                    MoveSelection(-1);
                    return true;

                case Key.U:
                    _ = _viewModel.ReloadAsync();
                    return true;

                case Key.I:
                    HideUnlessFailed(_viewModel.OpenSelectedLocation());
                    return true;

                case Key.Enter:
                    HideUnlessFailed(_viewModel.ExecuteSelected("runas"));
                    return true;

                case Key.PageDown:
                case Key.F:
                    _viewModel.SwitchTab(+1);
                    FocusActivePatternBox(selectAll: false);
                    return true;

                case Key.PageUp:
                    _viewModel.SwitchTab(-1);
                    FocusActivePatternBox(selectAll: false);
                    return true;

                default:
                    return false;
            }
        }

        private bool HandlePlainKey(Key key)
        {
            switch (key)
            {
                case Key.Enter:
                    HideUnlessFailed(_viewModel.ExecuteSelected());
                    return true;

                case Key.Escape:
                    Hide();
                    return true;

                default:
                    return false;
            }
        }

        private void MoveSelection(int delta)
        {
            _viewModel.MoveSelection(delta);

            var listBox = ActiveListBox;
            if (listBox.SelectedItem != null)
            {
                listBox.ScrollIntoView(listBox.SelectedItem);
            }
        }

        private void HideUnlessFailed(LaunchOutcome outcome)
        {
            if (outcome != LaunchOutcome.Failed)
            {
                Hide();
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // ユーザー操作による Close はキャンセルして非表示にする (終了はタスクトレイから)
            e.Cancel = true;
            Hide();
        }
    }
}
