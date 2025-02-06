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


    public class MyConfiguration
    {
        private string _settingFilename = "appsettings.json";

        public string HotKey { get; set; } = "Ctrl+Alt+O";

        public MyConfiguration(string filename)
        {
            _settingFilename = filename;

            var builder = new ConfigurationBuilder()
                .SetBasePath(System.IO.Directory.GetCurrentDirectory())
                .AddJsonFile(filename);

            var configuration = builder.Build();

            configuration.Bind(this);
        }

        public void Save()
        {
            string jsonString = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
            System.IO.File.WriteAllText(_settingFilename, jsonString);
        }
    }

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private HotKeyHelper _hotkey;
        private HotKeyData _nextHotKey = null;
        private HotKeyData _curHotKey = null;
        private MyConfiguration myconfig;

        public MainWindow()
        {
            InitializeComponent();

            myconfig = new MyConfiguration("appsettings.json");

            _nextHotKey = new HotKeyData(myconfig.HotKey);

            // HotKeyの登録
            this._hotkey = new HotKeyHelper(this);
            RegisterHotKey(_nextHotKey);
        }

        private void RegisterHotKey(HotKeyData hotkey)
        {
            this._hotkey.UnregisterAll();
            this._hotkey.Register(hotkey.ModifierKeys, hotkey.Key, HotKeyPressed);
            this._curHotKey = hotkey;

            this.HotKeyTextBlock.Content = _curHotKey.ToString();
            this.HotKeyTextBox.Text = hotkey.ToString();

            myconfig.HotKey = hotkey.ToString();
            myconfig.Save();
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

                if (cmd.Exec == "(ff)")
                {
                    execFileFilter(cmd.Args);
                    return;
                }

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

        private void execFileFilter(string args)
        {
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
                IEnumerable<string> files =
                    System.IO.Directory.EnumerateFiles(
                        args, "*", System.IO.SearchOption.AllDirectories);

                //ファイルを列挙する
                foreach (string f in files)
                {
                    sw.WriteLine(f);
                }
            }

            process.WaitForExit();

            var output = process.StandardOutput.ReadLine();
            if (!string.IsNullOrEmpty(output))
            {
                try
                {
                    Process.Start("explorer", output);
                }
                catch (System.Exception ex)
                {
                    MessageBox.Show(ex.Message, "実行エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                }

            }
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
            this._hotkey?.Dispose();
        }

        private void HideButton_Click(object sender, RoutedEventArgs e)
        {
            this.Hide();
        }

        private void SetHotKeyButton_Click(object sender, RoutedEventArgs e)
        {
            RegisterHotKey(_nextHotKey);
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