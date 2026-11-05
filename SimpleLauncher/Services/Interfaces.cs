using System.Diagnostics;
using SimpleLauncher.Config;
using SimpleLauncher.Models;

namespace SimpleLauncher.Services
{
    /// <summary>あいまい検索を提供するサービス。</summary>
    public interface IFilterService
    {
        Task SetListAsync(IReadOnlyList<string> items);

        Task SetCommandListAsync(IReadOnlyList<string> items);

        Task<IReadOnlyList<FilterResult>> FilterAsync(string pattern);
    }

    /// <summary>起動可能なアプリの一覧を列挙する。</summary>
    public interface IInstalledAppProvider
    {
        IReadOnlyDictionary<string, LaunchableItem> GetInstalledApps();
    }

    /// <summary>最近使ったファイルのパスを列挙する。</summary>
    public interface IRecentFilesProvider
    {
        IReadOnlyList<string> GetRecentFiles();
    }

    /// <summary>設定ファイルを読み込む。</summary>
    public interface ILauncherConfigProvider
    {
        LauncherConfig Load();
    }

    /// <summary>ショートカット (.lnk) のリンク先を解決する。</summary>
    public interface IShortcutResolver
    {
        /// <returns>リンク先パス。解決できない場合は null。</returns>
        string? GetTargetPath(string shortcutPath);
    }

    /// <summary>プロセスを起動する。</summary>
    public interface IProcessLauncher
    {
        void Start(ProcessStartInfo startInfo);
    }

    /// <summary>ユーザーへの通知。</summary>
    public interface IErrorReporter
    {
        void ShowError(string message);
    }
}
