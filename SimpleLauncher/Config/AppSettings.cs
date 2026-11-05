using Microsoft.Extensions.Configuration;

namespace SimpleLauncher.Config
{
    /// <summary>実行ファイルと同じ場所にある appsettings.json の内容。</summary>
    public sealed record AppSettings(string HotKey)
    {
        public const string FileName = "appsettings.json";
        public const string DefaultHotKey = "Ctrl+Alt+O";

        public static AppSettings Default { get; } = new(DefaultHotKey);

        /// <summary>指定ディレクトリの appsettings.json を読む。無ければ既定値。</summary>
        public static AppSettings Load(string directory)
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(directory)
                .AddJsonFile(FileName, optional: true, reloadOnChange: false)
                .Build();

            var hotKey = configuration["HotKey"];

            return new AppSettings(string.IsNullOrWhiteSpace(hotKey) ? DefaultHotKey : hotKey);
        }
    }
}
