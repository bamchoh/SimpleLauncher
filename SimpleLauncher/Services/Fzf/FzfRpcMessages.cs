using SimpleLauncher.Models;

namespace SimpleLauncher.Services.Fzf
{
    // fzf-filterd との JSON-RPC メッセージ。メンバー名は契約の一部なので変更しないこと。

    public class SetListArgs
    {
        public List<string> List { get; set; } = new();
    }

    public class SetListReply
    {
        public int Result { get; set; }
    }

    public class FilterArgs
    {
        public string Pattern { get; set; } = string.Empty;
    }

    public class FilterReply
    {
        public List<FilterResult> Results { get; set; } = new();
    }
}
