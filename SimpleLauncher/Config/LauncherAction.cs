namespace SimpleLauncher.Config
{
    /// <summary>
    /// 選択中の項目に対して行える操作。
    /// <see cref="Exec"/> が空なら項目そのものを <see cref="Verb"/> で起動し、
    /// そうでなければ <see cref="Exec"/> と <see cref="Args"/> のプレースホルダーを置換して実行する。
    /// </summary>
    /// <param name="Name">メニューに表示する名前。同名のアクションは後から定義したものが優先される。</param>
    /// <param name="Key">"Ctrl+T" のようなキー割り当て。省略可。</param>
    /// <param name="Exec">実行ファイル。alias と PATH で解決される。空なら項目自体を起動する。</param>
    /// <param name="Args">引数。</param>
    /// <param name="Verb">ShellExecute の動詞。"open" または "runas"。</param>
    public sealed record LauncherAction(string Name, string? Key, string? Exec, string Args, string Verb)
    {
        /// <summary>項目の実体 (ファイルやリンク先) のパスに置換される。</summary>
        public const string PathPlaceholder = "{path}";

        /// <summary>項目のあるフォルダ (項目自体がフォルダならそのフォルダ) に置換される。</summary>
        public const string DirPlaceholder = "{dir}";

        public const string DefaultVerb = "open";

        public static LauncherAction Open { get; } = new("開く", "Enter", null, string.Empty, DefaultVerb);

        public static LauncherAction RunAsAdmin { get; } = new("管理者として実行", "Ctrl+Enter", null, string.Empty, "runas");

        public static LauncherAction OpenLocation { get; } =
            new("場所を開く", "Ctrl+I", "explorer", $"/select,\"{PathPlaceholder}\"", DefaultVerb);

        /// <summary>設定に何も書かなくても使える組み込みアクション。</summary>
        public static IReadOnlyList<LauncherAction> Defaults { get; } = new[] { Open, RunAsAdmin, OpenLocation };

        /// <summary>項目そのものを起動するアクションか。</summary>
        public bool LaunchesItem => string.IsNullOrWhiteSpace(Exec);

        /// <summary>
        /// 組み込みアクションにユーザー定義を重ねる。同名なら同じ位置で置き換え、違えば末尾に追加する。
        /// 同じキーが複数に割り当てられていたら、後ろのアクションが勝ち、前のアクションはキー無しになる。
        /// </summary>
        public static List<LauncherAction> Merge(IEnumerable<LauncherAction> defaults, IEnumerable<LauncherAction> custom)
        {
            var merged = defaults.ToList();

            foreach (var action in custom)
            {
                var index = merged.FindIndex(a => string.Equals(a.Name, action.Name, StringComparison.Ordinal));
                if (index >= 0)
                {
                    merged[index] = action;
                }
                else
                {
                    merged.Add(action);
                }
            }

            var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var i = merged.Count - 1; i >= 0; i--)
            {
                var key = merged[i].Key;
                if (string.IsNullOrWhiteSpace(key))
                {
                    merged[i] = merged[i] with { Key = null };
                }
                else if (!seenKeys.Add(key))
                {
                    merged[i] = merged[i] with { Key = null };
                }
            }

            return merged;
        }
    }
}
