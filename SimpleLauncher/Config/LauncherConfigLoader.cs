using System.IO;
using YamlDotNet.Serialization;

namespace SimpleLauncher.Config
{
    /// <summary>YAML 文字列 / ファイルから <see cref="LauncherConfig"/> を生成する。</summary>
    public static class LauncherConfigLoader
    {
        private static readonly IDeserializer Deserializer =
            new DeserializerBuilder().IgnoreUnmatchedProperties().Build();

        /// <param name="yaml">YAML 本文。</param>
        /// <param name="configPath">--edit コマンドで開くパス。</param>
        public static LauncherConfig Parse(string yaml, string configPath)
        {
            var config = Deserializer.Deserialize<LauncherConfig>(yaml) ?? new LauncherConfig();
            config.Build(configPath);
            return config;
        }

        public static LauncherConfig Load(string filename) =>
            Parse(File.ReadAllText(filename), filename);
    }
}
