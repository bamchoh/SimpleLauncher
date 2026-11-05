namespace SimpleLauncher.Models
{
    /// <summary>一覧に表示する名前と、実際に起動するパスの組。</summary>
    public sealed record LaunchableItem(string DisplayName, string Path);
}
