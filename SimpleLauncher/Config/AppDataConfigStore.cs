using System.IO;
using SimpleLauncher.Services;

namespace SimpleLauncher.Config
{
    /// <summary>
    /// %APPDATA%\SimpleLauncher\simple-launcher.yaml を読み込む。
    /// 無ければ実行ファイルと同じ場所にある launcher.yaml をコピーして初期化する。
    /// </summary>
    public sealed class AppDataConfigStore : ILauncherConfigProvider
    {
        public const string ConfigFileName = "simple-launcher.yaml";
        public const string DefaultConfigFileName = "launcher.yaml";

        private readonly string _defaultConfigPath;

        public AppDataConfigStore()
            : this(
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SimpleLauncher"),
                Path.Combine(AppContext.BaseDirectory, DefaultConfigFileName))
        {
        }

        public AppDataConfigStore(string configDirectory, string defaultConfigPath)
        {
            ConfigDirectory = configDirectory;
            ConfigPath = Path.Combine(configDirectory, ConfigFileName);
            _defaultConfigPath = defaultConfigPath;
        }

        public string ConfigDirectory { get; }

        public string ConfigPath { get; }

        public LauncherConfig Load()
        {
            EnsureConfigExists();
            return LauncherConfigLoader.Load(ConfigPath);
        }

        private void EnsureConfigExists()
        {
            Directory.CreateDirectory(ConfigDirectory);

            if (File.Exists(ConfigPath))
            {
                return;
            }

            if (File.Exists(_defaultConfigPath))
            {
                File.Copy(_defaultConfigPath, ConfigPath);
            }
            else
            {
                File.WriteAllText(ConfigPath, "version: 1\nalias: {}\nlist: []\n");
            }
        }
    }
}
