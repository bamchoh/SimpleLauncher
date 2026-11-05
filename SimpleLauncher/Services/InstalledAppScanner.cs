using System.IO;
using SimpleLauncher.Models;

namespace SimpleLauncher.Services
{
    /// <summary>
    /// スタートメニューのショートカットとデスクトップ直下のファイルから起動対象を集める。
    /// </summary>
    public sealed class InstalledAppScanner : IInstalledAppProvider
    {
        private readonly IShortcutResolver _resolver;
        private readonly IReadOnlyList<string> _startMenuRoots;
        private readonly IReadOnlyList<string> _desktopRoots;

        public InstalledAppScanner(IShortcutResolver resolver)
            : this(resolver, DefaultStartMenuRoots(), DefaultDesktopRoots())
        {
        }

        public InstalledAppScanner(
            IShortcutResolver resolver,
            IEnumerable<string> startMenuRoots,
            IEnumerable<string> desktopRoots)
        {
            _resolver = resolver;
            _startMenuRoots = startMenuRoots.ToList();
            _desktopRoots = desktopRoots.ToList();
        }

        public IReadOnlyDictionary<string, LaunchableItem> GetInstalledApps()
        {
            var apps = new Dictionary<string, LaunchableItem>();

            foreach (var root in _startMenuRoots)
            {
                foreach (var lnk in EnumerateShortcuts(root))
                {
                    var displayName = Path.GetFileNameWithoutExtension(lnk);
                    if (IsLaunchableTarget(_resolver.GetTargetPath(lnk)) && !IsUninstaller(displayName))
                    {
                        apps[displayName] = new LaunchableItem(displayName, lnk);
                    }
                }
            }

            foreach (var root in _desktopRoots)
            {
                foreach (var file in EnumerateTopLevelFiles(root))
                {
                    // デスクトップのファイルはフルパスをそのまま表示名にする
                    apps[file] = new LaunchableItem(file, file);
                }
            }

            return apps;
        }

        /// <summary>exe もしくは UWP アプリを指すリンク先か。</summary>
        public static bool IsLaunchableTarget(string? target)
        {
            if (string.IsNullOrEmpty(target))
            {
                return false;
            }

            return target.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                || target.StartsWith("shell:AppsFolder", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsUninstaller(string displayName) =>
            displayName.Contains("uninstall", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// アクセスできないディレクトリをスキップしながら再帰的に .lnk を列挙する。
        /// </summary>
        public static IEnumerable<string> EnumerateShortcuts(string root)
        {
            if (!Directory.Exists(root))
            {
                yield break;
            }

            var stack = new Stack<string>();
            stack.Push(root);

            while (stack.Count > 0)
            {
                var dir = stack.Pop();

                string[] files;
                try
                {
                    files = Directory.GetFiles(dir, "*" + ShortcutFile.Extension);
                }
                catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
                {
                    continue;
                }

                foreach (var file in files)
                {
                    yield return file;
                }

                string[] subDirs;
                try
                {
                    subDirs = Directory.GetDirectories(dir);
                }
                catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
                {
                    continue;
                }

                foreach (var subDir in subDirs)
                {
                    stack.Push(subDir);
                }
            }
        }

        private static IEnumerable<string> EnumerateTopLevelFiles(string root)
        {
            if (!Directory.Exists(root))
            {
                return Array.Empty<string>();
            }

            try
            {
                return Directory.GetFiles(root);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                return Array.Empty<string>();
            }
        }

        private static IEnumerable<string> DefaultStartMenuRoots() => new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),
        };

        private static IEnumerable<string> DefaultDesktopRoots() => new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory),
        };
    }
}
