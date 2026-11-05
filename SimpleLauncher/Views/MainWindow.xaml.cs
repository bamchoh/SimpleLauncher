using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using SimpleLauncher.Config;
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
        private Dictionary<HotKeyData, LauncherAction> _actionsByKey = new();

        public MainWindow(MainWindowVM viewModel, HotKeyData hotKey)
        {
            InitializeComponent();

            _viewModel = viewModel;
            DataContext = viewModel;

            RebuildActionShortcuts();
            viewModel.PropertyChanged += ViewModel_PropertyChanged;

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

        private TextBox ActivePatternTextBox => _viewModel.IsActionMenuOpen
            ? ActionPatternTextBox
            : _viewModel.IsAppsTabActive ? PatternTextBox : RecentPatternTextBox;

        private ListBox ActiveListBox => _viewModel.IsActionMenuOpen
            ? ActionList
            : _viewModel.IsAppsTabActive ? FilteredResultList : RecentFilesList;

        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainWindowVM.Actions))
            {
                RebuildActionShortcuts();
            }
        }

        private void RebuildActionShortcuts()
        {
            var map = new Dictionary<HotKeyData, LauncherAction>();

            foreach (var action in _viewModel.Actions)
            {
                if (action.Key == null)
                {
                    continue;
                }

                if (HotKeyData.TryParse(action.Key, out var key))
                {
                    map[key] = action;
                }
                else
                {
                    Debug.WriteLine($"Ignoring invalid action key \"{action.Key}\" for \"{action.Name}\"");
                }
            }

            _actionsByKey = map;
        }

        private void HotKeyPressed(object? sender, EventArgs e)
        {
            _viewModel.CloseActionMenu();
            Show();
            Activate();
            FocusActivePatternBox(selectAll: true);
        }

        private void HideWindow()
        {
            _viewModel.CloseActionMenu();
            Hide();
        }

        private void FocusActivePatternBox(bool selectAll)
        {
            // タブ切替やメニュー表示の直後はコンテンツが生成されていないことがあるので、描画後にフォーカスする
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
            var key = e.Key == Key.System ? e.SystemKey : e.Key;
            var modifiers = Keyboard.Modifiers;

            e.Handled = _viewModel.IsActionMenuOpen
                ? HandleActionMenuKey(modifiers, key)
                : HandleListKey(modifiers, key);

            if (!e.Handled && _actionsByKey.TryGetValue(new HotKeyData(modifiers, key), out var action))
            {
                _viewModel.CloseActionMenu();
                HideUnlessFailed(_viewModel.RunAction(action));
                e.Handled = true;
            }
        }

        private bool HandleActionMenuKey(ModifierKeys modifiers, Key key)
        {
            if (modifiers == ModifierKeys.None)
            {
                switch (key)
                {
                    case Key.Escape:
                        _viewModel.CloseActionMenu();
                        FocusActivePatternBox(selectAll: false);
                        return true;

                    case Key.Enter:
                        HideUnlessFailed(_viewModel.RunSelectedAction());
                        return true;
                }
            }
            else if (modifiers == ModifierKeys.Control)
            {
                switch (key)
                {
                    case Key.N:
                        MoveSelection(+1);
                        return true;

                    case Key.P:
                        MoveSelection(-1);
                        return true;
                }
            }

            return false;
        }

        private bool HandleListKey(ModifierKeys modifiers, Key key)
        {
            if (modifiers == ModifierKeys.None)
            {
                switch (key)
                {
                    case Key.Escape:
                        HideWindow();
                        return true;

                    case Key.Tab:
                        if (_viewModel.OpenActionMenu())
                        {
                            FocusActivePatternBox(selectAll: false);
                        }

                        return true;
                }
            }
            else if (modifiers == ModifierKeys.Control)
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

                    case Key.PageDown:
                    case Key.F:
                        _viewModel.SwitchTab(+1);
                        FocusActivePatternBox(selectAll: false);
                        return true;

                    case Key.PageUp:
                        _viewModel.SwitchTab(-1);
                        FocusActivePatternBox(selectAll: false);
                        return true;
                }
            }

            return false;
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
                HideWindow();
            }
            else
            {
                FocusActivePatternBox(selectAll: false);
            }
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            // ユーザー操作による Close はキャンセルして非表示にする (終了はタスクトレイから)
            e.Cancel = true;
            HideWindow();
        }
    }
}
