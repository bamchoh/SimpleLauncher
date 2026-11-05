using System.Diagnostics;
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
    /// 一覧の項目から、起動に使う <see cref="ProcessStartInfo"/> を組み立てる。
    /// プロセスは起動しないので単体テストできる。
    /// </summary>
    public sealed class LaunchPlanner
    {
        private const string Explorer = "explorer.exe";

        private readonly IShortcutResolver _shortcutResolver;
        private readonly Func<string, string?> _findExecutable;

        public LaunchPlanner(IShortcutResolver shortcutResolver)
            : this(shortcutResolver, PathResolver.FindExecutableInPath)
        {
        }

        public LaunchPlanner(IShortcutResolver shortcutResolver, Func<string, string?> findExecutable)
        {
            _shortcutResolver = shortcutResolver;
            _findExecutable = findExecutable;
        }

        /// <summary>項目を関連付けアプリ / コマンドで起動するための情報。</summary>
        /// <param name="verb">"open" または "runas" など ShellExecute の動詞。</param>
        public ProcessStartInfo CreateLaunchInfo(FilterResult item, LaunchCatalog catalog, string verb = "open")
        {
            switch (item.Type)
            {
                case ItemKind.App:
                    return ShellOpen(FindApp(item, catalog).Path, verb);

                case ItemKind.File:
                    return ShellOpen(item.Text, verb);

                case ItemKind.Command:
                    var command = FindCommand(item, catalog);
                    var exec = catalog.Config.GetExecFromAlias(command.Exec);
                    return new ProcessStartInfo
                    {
                        FileName = _findExecutable(exec) ?? exec,
                        Arguments = command.Args,
                        UseShellExecute = true,
                        Verb = verb,
                    };

                default:
                    throw new LaunchException($"不明な項目種別です: {item.Type}");
            }
        }

        /// <summary>項目の場所をエクスプローラーで開くための情報。</summary>
        public ProcessStartInfo CreateOpenLocationInfo(FilterResult item, LaunchCatalog catalog)
        {
            switch (item.Type)
            {
                case ItemKind.App:
                    var path = FindApp(item, catalog).Path;
                    if (ShortcutFile.IsShortcut(path))
                    {
                        path = _shortcutResolver.GetTargetPath(path) ?? path;
                    }

                    return SelectInExplorer(path);

                case ItemKind.File:
                    return SelectInExplorer(item.Text);

                case ItemKind.Command:
                    var command = FindCommand(item, catalog);
                    var exec = catalog.Config.GetExecFromAlias(command.Exec);
                    var fileName = _findExecutable(exec) ?? exec;
                    if (!fileName.EndsWith(Explorer, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new LaunchException(
                            $"このコマンドではファイルの場所を開くことができません。 {command.Exec} : {command.Args}");
                    }

                    return SelectInExplorer(command.Args);

                default:
                    throw new LaunchException($"不明な項目種別です: {item.Type}");
            }
        }

        private static LaunchableItem FindApp(FilterResult item, LaunchCatalog catalog)
        {
            if (!catalog.Apps.TryGetValue(item.Text, out var app))
            {
                throw new LaunchException($"項目が見つかりません: {item.Text}");
            }

            return app;
        }

        private static Config.CommandInfo FindCommand(FilterResult item, LaunchCatalog catalog)
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

        private static ProcessStartInfo SelectInExplorer(string path)
        {
            // 既に引用符で囲まれている場合 (--edit コマンドなど) は二重にしない
            var quoted = path.StartsWith('"') ? path : $"\"{path}\"";

            return new ProcessStartInfo
            {
                FileName = Explorer,
                Arguments = $"/select,{quoted}",
                UseShellExecute = true,
            };
        }
    }
}
