using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;

namespace SimpleLauncher
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        // 内部変数
        private bool bOwnerShip = false;
        private Mutex? hMutex = null;

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            // 多重起動抑止 - 前処理
            var assembly = Assembly.GetExecutingAssembly();
            var guidAttr = assembly.GetCustomAttribute<GuidAttribute>();
            string mutexName = string.Format("{0}-{1}",
                                        Process.GetCurrentProcess().ProcessName,
                                        guidAttr?.Value ?? "00000000-1111-2222-3333-444444444444");
            hMutex = new Mutex(bOwnerShip, mutexName);

            try
            {
                bOwnerShip = hMutex.WaitOne(0);
            }
            catch (AbandonedMutexException)
            {
                // 正しく開放されずに破棄されていたという通知なので無視
                bOwnerShip = true;
            }
            catch { /* NOP */ }

            // 多重起動抑止 - 判定
            if (bOwnerShip)
            {
                // メインウィンドウ
                try
                {
                    var mainWindow = new MainWindow();
                    mainWindow.ShowInTaskbar = false;
                    mainWindow.Hide();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    Shutdown();
                }
            }
            else
            {
                hMutex.Close();
                hMutex = null;
                Shutdown();
            }
        }

        private void Application_Exit(object sender, ExitEventArgs e)
        {
            // 多重起動抑止 - 後処理
            if (hMutex != null)
            {
                if (bOwnerShip)
                {
                    // 所有権解放
                    hMutex.ReleaseMutex();
                }
                hMutex.Close();
            }
        }
    }

}
