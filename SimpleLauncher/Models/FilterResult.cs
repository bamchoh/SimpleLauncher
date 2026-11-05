namespace SimpleLauncher.Models
{
    /// <summary>
    /// <see cref="FilterResult.Type"/> に入る値。fzf-filterd との JSON 契約に合わせて文字列で保持する。
    /// </summary>
    public static class ItemKind
    {
        /// <summary>インストール済みアプリやデスクトップ上のファイル。</summary>
        public const string App = "list";

        /// <summary>設定 YAML に定義されたコマンド。</summary>
        public const string Command = "command";

        /// <summary>最近使ったファイル。</summary>
        public const string File = "file";
    }

    /// <summary>
    /// 一覧に表示される 1 項目。フィルタ結果として fzf-filterd から返されるほか、
    /// フィルタ無しの状態でも <see cref="Unmatched"/> で生成される。
    /// </summary>
    public class FilterResult
    {
        public string Type { get; set; } = ItemKind.App;

        public string Text { get; set; } = string.Empty;

        public int Score { get; set; }

        /// <summary>強調表示する文字位置。マッチ無しの場合は -1 のみ。</summary>
        public List<int> Pos { get; set; } = new();

        public override string ToString() => Text;

        public static FilterResult Unmatched(string type, string text) => new()
        {
            Type = type,
            Text = text,
            Score = 100,
            Pos = new List<int> { -1 },
        };
    }
}
