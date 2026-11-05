using System.IO;

namespace SimpleLauncher.Services
{
    /// <summary>
    /// 「最近使った項目」フォルダのショートカットを辿り、リンク先のパスを新しい順に返す。
    /// </summary>
    public sealed class RecentFilesScanner : IRecentFilesProvider
    {
        private readonly IShortcutResolver _resolver;
        private readonly string _recentDirectory;

        public RecentFilesScanner(IShortcutResolver resolver)
            : this(resolver, Environment.GetFolderPath(Environment.SpecialFolder.Recent))
        {
        }

        public RecentFilesScanner(IShortcutResolver resolver, string recentDirectory)
        {
            _resolver = resolver;
            _recentDirectory = recentDirectory;
        }

        public IReadOnlyList<string> GetRecentFiles()
        {
            if (!Directory.Exists(_recentDirectory))
            {
                return Array.Empty<string>();
            }

            var shortcuts = new DirectoryInfo(_recentDirectory)
                .EnumerateFiles("*" + ShortcutFile.Extension, SearchOption.TopDirectoryOnly)
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .Select(f => f.FullName);

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var result = new List<string>();

            foreach (var shortcut in shortcuts)
            {
                var target = _resolver.GetTargetPath(shortcut);
                if (!string.IsNullOrEmpty(target) && seen.Add(target))
                {
                    result.Add(target);
                }
            }

            return result;
        }
    }
}
