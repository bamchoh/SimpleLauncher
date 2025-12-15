using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using System.Configuration;
using System.Text.Json;
using System.Runtime.InteropServices;

namespace SimpleLauncher
{
    public class HotKeyData
    {
        public ModifierKeys ModifierKeys { get; set; }
        public Key Key { get; set; }

        public HotKeyData(string hotkeyStr)
        {
            try
            {
                foreach (var key in hotkeyStr.Split('+'))
                {
                    if (key == "Shift")
                    {
                        this.ModifierKeys |= ModifierKeys.Shift;
                    }
                    else if (key == "Ctrl")
                    {
                        this.ModifierKeys |= ModifierKeys.Control;
                    }
                    else if (key == "Alt")
                    {
                        this.ModifierKeys |= ModifierKeys.Alt;
                    }
                    else
                    {
                        this.Key = (Key)Enum.Parse(typeof(Key), key);
                    }
                }
            }
            catch (System.Exception ex)
            {
                throw new Exception(string.Format("Hotkey String \"{0}\" is invalid.\n{1}", hotkeyStr, ex.Message));
            }
        }

        public override string ToString()
        {
            var hotKeyList = new List<string>();

            if ((ModifierKeys & ModifierKeys.Shift) == ModifierKeys.Shift)
            {
                hotKeyList.Add("Shift");
            }

            if ((ModifierKeys & ModifierKeys.Control) == ModifierKeys.Control)
            {
                hotKeyList.Add("Ctrl");
            }

            if ((ModifierKeys & ModifierKeys.Alt) == ModifierKeys.Alt)
            {
                hotKeyList.Add("Alt");
            }

            hotKeyList.Add(Key.ToString());

            return string.Join("+", hotKeyList);
        }
    }



    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // Win32 API
        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        private HotKeyHelper _hotkey;
        private HotKeyData _nextHotKey = null;
        private HotKeyData _curHotKey = null;

        public MainWindow()
        {
            InitializeComponent();

            this.DataContext = new MainWindowVM();
            Loaded += (s, e) => PatternTextBox.Focus();

            _nextHotKey = new HotKeyData("Ctrl+Alt+O");

            // HotKeyの登録
            this._hotkey = new HotKeyHelper(this);
            RegisterHotKey(_nextHotKey);
        }

        private void RegisterHotKey(HotKeyData hotkey)
        {
            this._hotkey.UnregisterAll();
            this._hotkey.Register(hotkey.ModifierKeys, hotkey.Key, HotKeyPressed);
            this._curHotKey = hotkey;
        }

        private void HotKeyPressed(object? sender, EventArgs e)
        {
            if (this.DataContext == null)
                return;

            this.Show();

            this.Activate();

            PatternTextBox.Text = "";

            PatternTextBox.Focus();
        }

        private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            // Ctrl 押しながら
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                // Ctrl + J → 下移動
                if (e.Key == Key.N)
                {
                    if (FilteredResultList.SelectedIndex < FilteredResultList.Items.Count - 1)
                        FilteredResultList.SelectedIndex++;

                    FilteredResultList.ScrollIntoView(FilteredResultList.SelectedItem);
                    e.Handled = true;
                }

                // Ctrl + K → 上移動
                else if (e.Key == Key.P)
                {
                    if (FilteredResultList.SelectedIndex > 0)
                        FilteredResultList.SelectedIndex--;

                    FilteredResultList.ScrollIntoView(FilteredResultList.SelectedItem);
                    e.Handled = true;
                }

                else if (e.Key == Key.Enter)
                {
                    var vm = (MainWindowVM)this.DataContext;

                    try
                    {
                        if (vm.SelectedItem != null)
                        {
                            vm.Execute(vm.SelectedItem, "runas");
                        }
                        this.Hide();
                    }
                    catch (System.Exception ex)
                    {
                        System.Windows.MessageBox.Show(ex.Message, "実行エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                    }

                    e.Handled = true;
                }
            }
            else if (e.Key == Key.Enter)
            {
                var vm = (MainWindowVM)this.DataContext;
                if (vm.SelectedItem != null)
                {
                    vm.Execute(vm.SelectedItem);
                }

                this.Hide();

                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                this.Hide();
                e.Handled = true;
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // ユーザー操作による Close をキャンセル
            e.Cancel = true;

            // ウィンドウを非表示にする
            this.Hide();
        }
    }


    public class TextBlockPart
    {
        public string Text { get; set; } = "";
        public bool IsHighlighted { get; set; }
    }

    public class HookedTextBlock : TextBlock
    {
        static HookedTextBlock()
        {
            TextProperty.OverrideMetadata(
                typeof(HookedTextBlock),
                new FrameworkPropertyMetadata("", OnTextChanged)
            );
        }

        private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var self = (HookedTextBlock)d;

            var vm = self.DataContext as FilterResult;

            string text = (string)e.NewValue ?? "";
            self.Inlines.Clear();

            var splitTexts = SplitTextByHighlight(text, vm.Pos);

            foreach (var part in splitTexts)
            {
                if (part.IsHighlighted)
                {
                    self.Inlines.Add(new Run(part.Text) { FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Colors.Red) });
                }
                else
                {
                    self.Inlines.Add(new Run(part.Text));
                }
            }
        }

        private static List<TextBlockPart> SplitTextByHighlight(
            string fullText, List<int> highlightIndexes)
        {
            var result = new List<TextBlockPart>();
            if (string.IsNullOrEmpty(fullText)) return result;

            // 強調インデックスを高速に検索できる HashSet に
            var highlightSet = new HashSet<int>(highlightIndexes);

            int pos = 0;
            while (pos < fullText.Length)
            {
                bool currentIsHighlighted = highlightSet.Contains(pos);

                int start = pos;
                pos++;

                // 同じ種類（強調/通常）が続く間まとめる
                while (pos < fullText.Length &&
                        highlightSet.Contains(pos) == currentIsHighlighted)
                {
                    pos++;
                }

                string substring = fullText.Substring(start, pos - start);

                result.Add(new TextBlockPart
                {
                    Text = substring,
                    IsHighlighted = currentIsHighlighted
                });
            }

            return result;
        }
    }
}