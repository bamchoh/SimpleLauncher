using YamlDotNet.Serialization;

namespace SimpleLauncher.Config
{
    /// <summary>simple-launcher.yaml の内容。</summary>
    public class LauncherConfig
    {
        /// <summary>設定ファイルを開くための予約コマンド名。</summary>
        public const string EditCommandName = "--edit";

        [YamlMember(Alias = "version")]
        public int Version { get; set; }

        /// <summary>実行ファイル名の別名。キーが別名、値が実体。</summary>
        [YamlMember(Alias = "alias")]
        public Dictionary<string, string> Alias { get; set; } = new();

        /// <summary>
        /// コマンド定義。1 要素が 1 コマンドで、改行区切りで「名前 / 実行ファイル / 引数...」を並べる。
        /// </summary>
        [YamlMember(Alias = "list")]
        public List<string> List { get; set; } = new();

        /// <summary>選択中の項目に対するアクション定義。</summary>
        [YamlMember(Alias = "actions")]
        public List<ActionConfig> Actions { get; set; } = new();

        [YamlIgnore]
        public Dictionary<string, CommandInfo> CommandList { get; private set; } = new();

        /// <summary>組み込みアクションと <see cref="Actions"/> をマージしたもの。</summary>
        [YamlIgnore]
        public IReadOnlyList<LauncherAction> ActionList { get; private set; } = LauncherAction.Defaults;

        /// <summary>
        /// <see cref="List"/> と <see cref="Actions"/> を解析して
        /// <see cref="CommandList"/> と <see cref="ActionList"/> を組み立てる。
        /// </summary>
        /// <param name="configPath"><see cref="EditCommandName"/> で開く設定ファイルのパス。</param>
        public void Build(string configPath)
        {
            BuildCommandList(configPath);
            BuildActionList();
        }

        public string GetExecFromAlias(string exec) =>
            Alias.TryGetValue(exec, out var resolved) ? resolved : exec;

        private void BuildCommandList(string configPath)
        {
            var commands = new Dictionary<string, CommandInfo>
            {
                [EditCommandName] = new CommandInfo(EditCommandName, "explorer", $"\"{configPath}\""),
            };

            foreach (var entry in List)
            {
                var command = CommandInfo.Parse(entry);
                if (command != null)
                {
                    commands[command.Name] = command;
                }
            }

            CommandList = commands;
        }

        private void BuildActionList()
        {
            var custom = Actions
                .Select(a => a.ToAction())
                .Where(a => a != null)
                .Select(a => a!);

            ActionList = LauncherAction.Merge(LauncherAction.Defaults, custom);
        }
    }

    public sealed record CommandInfo(string Name, string Exec, string Args)
    {
        /// <summary>
        /// 「名前\n実行ファイル\n引数...」形式の文字列を解析する。2 行未満なら null。
        /// </summary>
        public static CommandInfo? Parse(string entry)
        {
            var lines = entry.Split('\n');
            if (lines.Length < 2)
            {
                return null;
            }

            var args = lines.Length >= 3 ? string.Join("\n", lines.Skip(2)) : string.Empty;
            return new CommandInfo(lines[0], lines[1], args);
        }
    }

    /// <summary>YAML の actions 要素。</summary>
    public class ActionConfig
    {
        [YamlMember(Alias = "name")]
        public string? Name { get; set; }

        [YamlMember(Alias = "key")]
        public string? Key { get; set; }

        [YamlMember(Alias = "exec")]
        public string? Exec { get; set; }

        [YamlMember(Alias = "args")]
        public string? Args { get; set; }

        [YamlMember(Alias = "verb")]
        public string? Verb { get; set; }

        /// <returns>名前が無い場合は null。</returns>
        public LauncherAction? ToAction()
        {
            if (string.IsNullOrWhiteSpace(Name))
            {
                return null;
            }

            return new LauncherAction(
                Name.Trim(),
                string.IsNullOrWhiteSpace(Key) ? null : Key.Trim(),
                string.IsNullOrWhiteSpace(Exec) ? null : Exec.Trim(),
                Args ?? string.Empty,
                string.IsNullOrWhiteSpace(Verb) ? LauncherAction.DefaultVerb : Verb.Trim());
        }
    }
}
