using System.Diagnostics;
using System.IO;
using SimpleLauncher.Config;
using SimpleLauncher.Models;

namespace SimpleLauncher.Services
{
    /// <summary>起動できない項目を選んだときに投げられる。メッセージはそのままユーザーに表示できる。</summary>
    public sealed class LaunchException : Exception
    {
        public LaunchException(string message) : base(message)
        {
        }
    }

    /// <summary>
    /// 一覧の項目とアクションから、起動に使う <see cref="ProcessStartInfo"/> を組み立てる。
    /// プロセスは起動しないので単体テストできる。
    /// </summary>
    public sealed class LaunchPlanner
    {
        private readonly IShortcutResolver _shortcutResolver;
        private readonly Func<string, string?> _findExecutable;
        private readonly Func<string, bool> _isDirectory;

        public LaunchPlanner(IShortcutResolver shortcutResolver)
            : this(shortcutResolver, PathResolver.FindExecutableInPath, Directory.Exists)
        {
        }

        public LaunchPlanner(IShortcutResolver shortcutResolver, Func<string, string?> findExecutable, Func<string, bool> isDirectory)
        {
            _shortcutResolver = shortcutResolver;
            _findExecutable = findExecutable;
            _isDirectory = isDirectory;
        }

        /// <summary>項目を関連付けアプリ / コマンドで起動するための情報。</summary>
        /// <param name="verb">"open" または "runas" など ShellExecute の動詞。</param>
        public ProcessStartInfo CreateLaunchInfo(FilterResult item, LaunchCatalog catalog, string verb = LauncherAction.DefaultVerb)
        {
            switch (item.Type)
            {
                case ItemKind.App:
                    return ShellOpen(FindApp(item, catalog).Path, verb);

                case ItemKind.File:
                    return ShellOpen(item.Text, verb);

                case ItemKind.Command:
                    var command = FindCommand(item, catalog);
                    return new ProcessStartInfo
                    {
                        FileName = ResolveExecutable(command.Exec, catalog),
                        Arguments = command.Args,
                        UseShellExecute = true,
                        Verb = verb,
                    };

                default:
                    throw new LaunchException($"不明な項目種別です: {item.Type}");
            }
        }

        /// <summary>アクションを項目に適用するための情報。</summary>
        public ProcessStartInfo CreateActionInfo(LauncherAction action, FilterResult item, LaunchCatalog catalog)
        {
            if (action.LaunchesItem)
            {
                return CreateLaunchInfo(item, catalog, action.Verb);
            }

            var path = ResolveItemPath(item, catalog);
            var dir = _isDirectory(path) ? path : Path.GetDirectoryName(path) ?? path;

            var exec = Substitute(action.Exec!, path, dir);

            return new ProcessStartInfo
            {
                FileName = ResolveExecutable(exec, catalog),
                Arguments = Substitute(action.Args, path, dir),
                UseShellExecute = true,
                Verb = action.Verb,
            };
        }

        /// <summary>
        /// 項目の実体のパス。アプリはショートカットのリンク先、ファイルはそのパス、
        /// コマンドはエクスプローラーを開くものに限りその引数。
        /// </summary>
        public string ResolveItemPath(FilterResult item, LaunchCatalog catalog)
        {
            switch (item.Type)
            {
                case ItemKind.App:
                    var path = FindApp(item, catalog).Path;
                    if (ShortcutFile.IsShortcut(path))
                    {
                        path = _shortcutResolver.GetTargetPath(path) ?? path;
                    }

                    return path;

                case ItemKind.File:
                    return item.Text;

                case ItemKind.Command:
                    var command = FindCommand(item, catalog);
                    if (!IsExplorer(ResolveExecutable(command.Exec, catalog)))
                    {
                        throw new LaunchException(
                            $"このコマンドにはパスがありません。 {command.Exec} : {command.Args}");
                    }

                    return TrimQuotes(command.Args);

                default:
                    throw new LaunchException($"不明な項目種別です: {item.Type}");
            }
        }

        private string ResolveExecutable(string exec, LaunchCatalog catalog)
        {
            var resolved = catalog.Config.GetExecFromAlias(exec);
            return _findExecutable(resolved) ?? resolved;
        }

        private static bool IsExplorer(string executable) =>
            string.Equals(Path.GetFileNameWithoutExtension(executable), "explorer", StringComparison.OrdinalIgnoreCase);

        private static string Substitute(string template, string path, string dir) =>
            template
                .Replace(LauncherAction.PathPlaceholder, path, StringComparison.OrdinalIgnoreCase)
                .Replace(LauncherAction.DirPlaceholder, dir, StringComparison.OrdinalIgnoreCase);

        private static string TrimQuotes(string value)
        {
            var trimmed = value.Trim();
            return trimmed.Length >= 2 && trimmed[0] == '"' && trimmed[^1] == '"'
                ? trimmed[1..^1]
                : trimmed;
        }

        private static LaunchableItem FindApp(FilterResult item, LaunchCatalog catalog)
        {
            if (!catalog.Apps.TryGetValue(item.Text, out var app))
            {
                throw new LaunchException($"項目が見つかりません: {item.Text}");
            }

            return app;
        }

        private static CommandInfo FindCommand(FilterResult item, LaunchCatalog catalog)
        {
            if (!catalog.Config.CommandList.TryGetValue(item.Text, out var command))
            {
                throw new LaunchException($"コマンドが見つかりません: {item.Text}");
            }

            return command;
        }

        private static ProcessStartInfo ShellOpen(string path, string verb) => new()
        {
            FileName = path,
            UseShellExecute = true,
            Verb = verb,
        };
    }
}
