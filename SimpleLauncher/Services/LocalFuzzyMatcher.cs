using SimpleLauncher.Models;

namespace SimpleLauncher.Services
{
    /// <summary>
    /// fzf-filterd を介さずにプロセス内で行う簡易あいまい検索。
    /// 空白区切りの各語について、連続一致を優先し、無ければ部分列一致を探す。
    /// </summary>
    public static class LocalFuzzyMatcher
    {
        public const int MaxScore = 100;

        /// <summary>全項目を検索し、スコア順に並べて返す。パターンが空なら全件をそのまま返す。</summary>
        public static List<FilterResult> Filter(string type, IEnumerable<string> items, string pattern)
        {
            if (string.IsNullOrWhiteSpace(pattern))
            {
                return items.Select(text => FilterResult.Unmatched(type, text)).ToList();
            }

            var results = items
                .Select(text => Match(type, text, pattern))
                .Where(result => result != null)
                .Select(result => result!)
                .ToList();

            FilterResultSorter.SortByScore(results);
            return results;
        }

        /// <summary>1 件を検索する。マッチしなければ null。</summary>
        public static FilterResult? Match(string type, string text, string pattern)
        {
            var terms = pattern.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (terms.Length == 0)
            {
                return FilterResult.Unmatched(type, text);
            }

            var positions = new SortedSet<int>();
            var totalScore = 0;

            foreach (var term in terms)
            {
                var termMatch = MatchTerm(text, term);
                if (termMatch == null)
                {
                    return null;
                }

                positions.UnionWith(termMatch.Value.Positions);
                totalScore += termMatch.Value.Score;
            }

            return new FilterResult
            {
                Type = type,
                Text = text,
                Score = totalScore / terms.Length,
                Pos = positions.ToList(),
            };
        }

        private static (IReadOnlyList<int> Positions, int Score)? MatchTerm(string text, string term)
        {
            // 連続一致: ファイル名側 (末尾) を優先するため最後の出現位置を使う
            var index = text.LastIndexOf(term, StringComparison.OrdinalIgnoreCase);
            if (index >= 0)
            {
                return (Enumerable.Range(index, term.Length).ToList(), MaxScore);
            }

            // 部分列一致
            var positions = new List<int>(term.Length);
            var searchFrom = 0;
            foreach (var ch in term)
            {
                var found = IndexOfIgnoreCase(text, ch, searchFrom);
                if (found < 0)
                {
                    return null;
                }

                positions.Add(found);
                searchFrom = found + 1;
            }

            // 一致範囲が狭いほど高得点。連続一致 (span == length) は上で処理済みなので必ず MaxScore 未満になる
            var span = positions[^1] - positions[0] + 1;
            var score = MaxScore * term.Length / span;
            return (positions, Math.Min(score, MaxScore - 1));
        }

        private static int IndexOfIgnoreCase(string text, char ch, int startIndex)
        {
            for (var i = startIndex; i < text.Length; i++)
            {
                if (char.ToUpperInvariant(text[i]) == char.ToUpperInvariant(ch))
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
