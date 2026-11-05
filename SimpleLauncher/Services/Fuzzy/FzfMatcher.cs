namespace SimpleLauncher.Services.Fuzzy
{
    /// <summary>1 件のあいまい一致の結果。</summary>
    /// <param name="Start">一致範囲の開始 (文字インデックス)。</param>
    /// <param name="End">一致範囲の終了 (排他的)。</param>
    /// <param name="Score">スコア。大きいほど良い。</param>
    /// <param name="Positions">パターンの各文字が一致した位置 (昇順)。</param>
    public sealed record FuzzyMatch(int Start, int End, int Score, IReadOnlyList<int> Positions);

    /// <summary>
    /// fzf (junegunn/fzf, src/algo/algo.go) の FuzzyMatchV2 の移植。
    /// Smith-Waterman の変形で、パターンの各文字を省略せずにテキスト中へ最適配置し、
    /// 語頭・camelCase・連続一致にボーナス、間隔にペナルティを与えてスコアを決める。
    /// 文字単位 (UTF-16) で動作する。
    /// </summary>
    public static class FzfMatcher
    {
        public const int ScoreMatch = 16;
        public const int ScoreGapStart = -3;
        public const int ScoreGapExtension = -1;

        /// <summary>語頭のボーナス。間隔が 8 文字を超えるとペナルティで相殺される値。</summary>
        public const int BonusBoundary = ScoreMatch / 2;

        /// <summary>記号など語を構成しない文字のボーナス。</summary>
        public const int BonusNonWord = ScoreMatch / 2;

        /// <summary>camelCase の大文字や、英字直後の数字へのボーナス。</summary>
        public const int BonusCamel123 = BonusBoundary + ScoreGapExtension;

        /// <summary>連続して一致した文字に最低限与えるボーナス。</summary>
        public const int BonusConsecutive = -(ScoreGapStart + ScoreGapExtension);

        /// <summary>パターン先頭の文字は重要なのでボーナスを倍にする。</summary>
        public const int BonusFirstCharMultiplier = 2;

        /// <summary>fzf はこれより長いパターンを V1 にフォールバックする。ここでは不一致扱いにする。</summary>
        public const int MaxPatternLength = 1000;

        /// <summary>fzf のスマートケース: パターンに大文字が含まれるときだけ大文字小文字を区別する。</summary>
        public static bool IsCaseSensitive(string pattern) => pattern.Any(char.IsUpper);

        /// <param name="text">検索対象。</param>
        /// <param name="pattern">パターン。<paramref name="caseSensitive"/> が false なら小文字化して比較する。</param>
        /// <param name="caseSensitive">大文字小文字を区別するか。</param>
        /// <param name="forward">
        /// 同点のとき前方 (true) と後方 (false) のどちらの出現を採用するか。
        /// fzf-filterd は false で呼んでいたので既定も false。
        /// </param>
        /// <param name="scheme">境界ボーナスの重み。null なら <see cref="FzfScheme.Default"/>。</param>
        /// <returns>一致しなければ null。空パターンは Score 0 の一致を返す。</returns>
        public static FuzzyMatch? Match(string text, string pattern, bool caseSensitive, bool forward = false, FzfScheme? scheme = null)
        {
            scheme ??= FzfScheme.Default;

            var m = pattern.Length;
            if (m == 0)
            {
                return new FuzzyMatch(0, 0, 0, Array.Empty<int>());
            }

            var n = text.Length;
            if (m > n || m > MaxPatternLength)
            {
                return null;
            }

            if (!caseSensitive)
            {
                pattern = pattern.ToLowerInvariant();
            }

            // Phase 1. 貪欲に部分列を探して探索範囲 [minIdx, maxIdx) を絞る
            if (!TryFindWindow(text, pattern, caseSensitive, out var minIdx, out var maxIdx))
            {
                return null;
            }

            n = maxIdx - minIdx;

            var t = new char[n];
            text.CopyTo(minIdx, t, 0, n);

            var h0 = new int[n];
            var c0 = new int[n];
            var bonusAt = new int[n];
            var firstOccurrence = new int[m];

            // Phase 2. 各位置のボーナスと、パターン 1 文字目の行 (H0) を計算する
            int maxScore = 0, maxScorePos = 0;
            int pidx = 0, lastIdx = 0;
            char pchar0 = pattern[0], pchar = pattern[0];
            var prevH0 = 0;
            var prevClass = scheme.InitialCharClass;
            var inGap = false;

            for (var off = 0; off < n; off++)
            {
                var ch = t[off];
                var cls = scheme.ClassOf(ch);
                if (!caseSensitive && cls == CharClass.Upper)
                {
                    ch = char.ToLowerInvariant(ch);
                    t[off] = ch;
                }

                var bonus = scheme.Bonus(prevClass, cls);
                bonusAt[off] = bonus;
                prevClass = cls;

                if (ch == pchar)
                {
                    if (pidx < m)
                    {
                        firstOccurrence[pidx] = off;
                        pidx++;
                        pchar = pattern[Math.Min(pidx, m - 1)];
                    }

                    lastIdx = off;
                }

                if (ch == pchar0)
                {
                    var score = ScoreMatch + bonus * BonusFirstCharMultiplier;
                    h0[off] = score;
                    c0[off] = 1;
                    if (m == 1 && (forward ? score > maxScore : score >= maxScore))
                    {
                        maxScore = score;
                        maxScorePos = off;
                        if (forward && bonus >= BonusBoundary)
                        {
                            break;
                        }
                    }

                    inGap = false;
                }
                else
                {
                    h0[off] = inGap
                        ? Math.Max(prevH0 + ScoreGapExtension, 0)
                        : Math.Max(prevH0 + ScoreGapStart, 0);
                    c0[off] = 0;
                    inGap = true;
                }

                prevH0 = h0[off];
            }

            if (pidx != m)
            {
                return null;
            }

            if (m == 1)
            {
                var pos = minIdx + maxScorePos;
                return new FuzzyMatch(pos, pos + 1, maxScore, new[] { pos });
            }

            // Phase 3. スコア行列 H を埋める (省略は許さない)
            var f0 = firstOccurrence[0];
            var width = lastIdx - f0 + 1;
            var h = new int[width * m];
            var c = new int[width * m];
            Array.Copy(h0, f0, h, 0, width);
            Array.Copy(c0, f0, c, 0, width);

            for (pidx = 1; pidx < m; pidx++)
            {
                var f = firstOccurrence[pidx];
                pchar = pattern[pidx];
                var row = pidx * width;
                inGap = false;

                for (var col = f; col <= lastIdx; col++)
                {
                    var idx = row + col - f0;
                    var left = col == f ? 0 : h[idx - 1];
                    var s2 = left + (inGap ? ScoreGapExtension : ScoreGapStart);
                    var s1 = 0;
                    var consecutive = 0;

                    if (pchar == t[col])
                    {
                        s1 = h[idx - 1 - width] + ScoreMatch;
                        var b = bonusAt[col];
                        consecutive = c[idx - 1 - width] + 1;
                        if (consecutive > 1)
                        {
                            var firstBonus = bonusAt[col - consecutive + 1];
                            if (b >= BonusBoundary && b > firstBonus)
                            {
                                // より強い境界で連続チャンクを切る
                                consecutive = 1;
                            }
                            else
                            {
                                b = Math.Max(b, Math.Max(BonusConsecutive, firstBonus));
                            }
                        }

                        if (s1 + b < s2)
                        {
                            s1 += bonusAt[col];
                            consecutive = 0;
                        }
                        else
                        {
                            s1 += b;
                        }
                    }

                    c[idx] = consecutive;
                    inGap = s1 < s2;
                    var score = Math.Max(Math.Max(s1, s2), 0);
                    if (pidx == m - 1 && (forward ? score > maxScore : score >= maxScore))
                    {
                        maxScore = score;
                        maxScorePos = col;
                    }

                    h[idx] = score;
                }
            }

            // Phase 4. 逆方向にたどって一致位置を求める
            var positions = new int[m];
            var i = m - 1;
            var j = maxScorePos;
            var preferMatch = true;

            while (true)
            {
                var rowStart = i * width;
                var j0 = j - f0;
                var s = h[rowStart + j0];

                var diag = 0;
                var leftScore = 0;
                if (i > 0 && j >= firstOccurrence[i])
                {
                    diag = h[rowStart - width + j0 - 1];
                }

                if (j > firstOccurrence[i])
                {
                    leftScore = h[rowStart + j0 - 1];
                }

                var row = i;
                if (s > diag && (s > leftScore || (s == leftScore && preferMatch)))
                {
                    positions[i] = j + minIdx;
                    if (i == 0)
                    {
                        break;
                    }

                    i--;
                }

                // 次の行は F[row+1] 以降しか書かれていないので、その左は読まない
                preferMatch = c[rowStart + j0] > 1
                    || (row + 1 < m && j < lastIdx && j + 1 >= firstOccurrence[row + 1] && c[rowStart + width + j0 + 1] > 0);
                j--;
            }

            return new FuzzyMatch(minIdx + j, minIdx + maxScorePos + 1, maxScore, positions);
        }

        /// <summary>
        /// パターンを貪欲に部分列として探し、見つかれば最適配置が必ず収まる範囲を返す。
        /// 範囲の先頭は 1 文字分戻してあり、語頭ボーナスの判定に使う。
        /// </summary>
        private static bool TryFindWindow(string text, string pattern, bool caseSensitive, out int minIdx, out int maxIdx)
        {
            minIdx = 0;
            maxIdx = 0;

            var idx = 0;
            var lastIdx = 0;
            for (var pidx = 0; pidx < pattern.Length; pidx++)
            {
                idx = IndexOf(text, pattern[pidx], idx, caseSensitive);
                if (idx < 0)
                {
                    return false;
                }

                if (pidx == 0 && idx > 0)
                {
                    minIdx = idx - 1;
                }

                lastIdx = idx;
                idx++;
            }

            var end = LastIndexOf(text, pattern[^1], lastIdx + 1, caseSensitive);
            maxIdx = end >= 0 ? end + 1 : lastIdx + 1;
            return true;
        }

        private static int IndexOf(string text, char patternChar, int from, bool caseSensitive)
        {
            for (var i = from; i < text.Length; i++)
            {
                if (Equals(text[i], patternChar, caseSensitive))
                {
                    return i;
                }
            }

            return -1;
        }

        private static int LastIndexOf(string text, char patternChar, int from, bool caseSensitive)
        {
            for (var i = text.Length - 1; i >= from; i--)
            {
                if (Equals(text[i], patternChar, caseSensitive))
                {
                    return i;
                }
            }

            return -1;
        }

        private static bool Equals(char textChar, char patternChar, bool caseSensitive) =>
            caseSensitive ? textChar == patternChar : char.ToLowerInvariant(textChar) == patternChar;
    }
}
