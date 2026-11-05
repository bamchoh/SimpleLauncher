using SimpleLauncher.Models;

namespace SimpleLauncher.Services.Fuzzy
{
    /// <summary><see cref="FzfMatcher"/> で一覧を絞り込み、<see cref="FilterResult"/> に変換する。</summary>
    public static class FuzzyFilter
    {
        /// <summary>
        /// 一致した項目だけをスコア順に返す。パターンが空なら全件をそのまま返す。
        /// 大文字小文字の扱いは fzf のスマートケースに従う。
        /// </summary>
        public static List<FilterResult> Filter(string type, IEnumerable<string> items, string pattern, FzfScheme? scheme = null)
        {
            if (string.IsNullOrEmpty(pattern))
            {
                return items.Select(text => FilterResult.Unmatched(type, text)).ToList();
            }

            var caseSensitive = FzfMatcher.IsCaseSensitive(pattern);
            var results = new List<FilterResult>();

            foreach (var text in items)
            {
                var match = FzfMatcher.Match(text, pattern, caseSensitive, forward: false, scheme);
                if (match != null && match.Score > 0)
                {
                    results.Add(new FilterResult
                    {
                        Type = type,
                        Text = text,
                        Score = match.Score,
                        Pos = match.Positions.ToList(),
                    });
                }
            }

            FilterResultSorter.SortByScore(results);
            return results;
        }
    }
}
