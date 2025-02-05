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

namespace SimpleLauncher
{
    public class HotKeyData
    {
        public ModifierKeys ModifierKeys { get; set; }
        public Key Key { get; set; }

        public HotKeyData()
        {
            this.ModifierKeys = ModifierKeys.Control | ModifierKeys.Alt;
            this.Key = Key.O;
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
        private HotKeyHelper _hotkey;
        private HotKeyData _nextHotKey = new HotKeyData();
        private HotKeyData _curHotKey = new HotKeyData();

        public MainWindow()
        {
            InitializeComponent();

            // HotKeyの登録
            this._hotkey = new HotKeyHelper(this);
            RegisterHotKey();
        }

        private void RegisterHotKey()
        {
            this._hotkey.UnregisterAll();
            this._hotkey.Register(_nextHotKey.ModifierKeys, _nextHotKey.Key, HotKeyPressed);
            this._curHotKey = _nextHotKey;

            this.HotKeyTextBlock.Content = _curHotKey.ToString();
            this.HotKeyTextBox.Text = _nextHotKey.ToString();
        }

        private void HotKeyPressed(object? sender, EventArgs e)
        {
            var appdir = System.IO.Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SimpleLauncher");

            if (!System.IO.Directory.Exists(appdir))
            {
                System.IO.Directory.CreateDirectory(appdir);
            }

            var confpath = System.IO.Path.Join(appdir, "simple-launcher.yaml");

            var yaml = YamlLoadUtil.Load(confpath);

            var app = new ProcessStartInfo();
            app.FileName = "cmd";
            app.Arguments = "/c fzf";
            app.StandardInputEncoding = Encoding.UTF8;
            app.StandardOutputEncoding = Encoding.UTF8;
            app.StandardErrorEncoding = Encoding.UTF8;
            app.RedirectStandardInput = true;
            app.RedirectStandardOutput = true;
            app.RedirectStandardError = true;
            app.UseShellExecute = false;

            var process = Process.Start(app);
            if (process == null)
            {
                throw new System.Exception("Failed to start process.");
            }

            using (var sw = process.StandardInput)
            {
                foreach (var item in yaml.CommandList.Keys)
                {
                    sw.WriteLine(item);
                }
            }

            process.WaitForExit();

            var output = process.StandardOutput.ReadLine();
            if (!string.IsNullOrEmpty(output))
            {
                if (output == "--show setting")
                {
                    this.WindowState = WindowState.Normal;
                    this.Show();
                    return;
                }

                var cmd = yaml.CommandList[output];
                try
                {
                    Process.Start(yaml.GetExecFromAlias(cmd.Exec), cmd.Args);
                } catch (System.Exception ex)
                {
                    MessageBox.Show(ex.Message, "実行エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }

            var errout = process.StandardError.ReadLine();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            // ウィンドウを閉じます。
            Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            // HotKeyの登録解除
            this._hotkey.Dispose();
        }

        private void HideButton_Click(object sender, RoutedEventArgs e)
        {
            this.Hide();
        }

        private void SetHotKeyButton_Click(object sender, RoutedEventArgs e)
        {
            RegisterHotKey();
        }

        private void HotKeyTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if(e.Key == Key.Tab || e.Key == Key.Enter || e.Key == Key.Escape)
            {
                return;
            }

            if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
            {
                _nextHotKey.ModifierKeys |= ModifierKeys.Shift;
            }
            else
            {
                _nextHotKey.ModifierKeys &= ~ModifierKeys.Shift;
            }

            if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                _nextHotKey.ModifierKeys |= ModifierKeys.Control;
            }
            else
            {
                _nextHotKey.ModifierKeys &= ~ModifierKeys.Control;
            }

            if ((Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt)
            {
                _nextHotKey.ModifierKeys |= ModifierKeys.Alt;
            }
            else
            {
                _nextHotKey.ModifierKeys &= ~ModifierKeys.Alt;
            }

            if (e.Key >= Key.A && e.Key <= Key.Z)
            {
                _nextHotKey.Key = e.Key;
            }

            this.HotKeyTextBox.Text = _nextHotKey.ToString();

            e.Handled = true;
        }

        private void HotKeyTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            _nextHotKey = _curHotKey;
            this.HotKeyTextBox.Text = _nextHotKey.ToString();
        }

    }
}