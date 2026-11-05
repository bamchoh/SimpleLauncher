using SimpleLauncher.Models;

namespace SimpleLauncher.Services
{
    public static class FilterResultSorter
    {
        /// <summary>表示名の辞書順 (大文字小文字を区別しない)。フィルタ無しの一覧に使う。</summary>
        public static void SortByText(List<FilterResult> results)
        {
            results.Sort((a, b) => string.Compare(a.Text, b.Text, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>スコアの高い順。同点なら短い名前を優先し、さらに同じなら辞書順で安定させる。</summary>
        public static void SortByScore(List<FilterResult> results)
        {
            results.Sort((a, b) =>
            {
                var byScore = b.Score.CompareTo(a.Score);
                if (byScore != 0)
                {
                    return byScore;
                }

                var byLength = a.Text.Length.CompareTo(b.Text.Length);
                if (byLength != 0)
                {
                    return byLength;
                }

                return string.Compare(a.Text, b.Text, StringComparison.Ordinal);
            });
        }
    }
}
