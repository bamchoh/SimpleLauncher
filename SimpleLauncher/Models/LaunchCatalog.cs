using SimpleLauncher.Config;

namespace SimpleLauncher.Models
{
    /// <summary>起動対象を解決するために必要な情報のスナップショット。</summary>
    public sealed record LaunchCatalog(
        IReadOnlyDictionary<string, LaunchableItem> Apps,
        LauncherConfig Config)
    {
        public static LaunchCatalog Empty { get; } =
            new(new Dictionary<string, LaunchableItem>(), new LauncherConfig());
    }
}
