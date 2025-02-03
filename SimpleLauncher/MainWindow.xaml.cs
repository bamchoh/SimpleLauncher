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
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private HotKeyHelper _hotkey;

        public MainWindow()
        {
            InitializeComponent();

            // HotKeyの登録
            this._hotkey = new HotKeyHelper(this);
            this._hotkey.Register(ModifierKeys.Control | ModifierKeys.Alt, Key.O, HotKeyPressed);
        }

        private async void HotKeyPressed(object? sender, EventArgs e)
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
                var cmd = yaml.CommandList[output];
                Process.Start(cmd.Exec, cmd.Args);
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

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }
    }
}