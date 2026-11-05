using SimpleLauncher.Services.Fuzzy;
using static SimpleLauncher.Services.Fuzzy.FzfMatcher;

namespace SimpleLauncher.Tests
{
    /// <summary>
    /// fzf 本体の src/algo/algo_test.go (TestFuzzyMatch 等) から移植した期待値で、移植の忠実さを検証する。
    /// </summary>
    public class FzfMatcherTests
    {
        // default スキームの境界ボーナス (fzf: bonusBoundaryWhite / bonusBoundaryDelimiter)
        private const int BBW = BonusBoundary + 2;
        private const int BBD = BonusBoundary + 1;

        public static IEnumerable<object[]> FuzzyMatchCases()
        {
            // caseSensitive, input, pattern, start, end, score
            yield return Case(false, "fooBarbaz1", "oBZ", 2, 9,
                ScoreMatch * 3 + BonusCamel123 + ScoreGapStart + ScoreGapExtension * 3);
            yield return Case(false, "foo bar baz", "fbb", 0, 9,
                ScoreMatch * 3 + BBW * BonusFirstCharMultiplier + BBW * 2 + 2 * ScoreGapStart + 4 * ScoreGapExtension);
            yield return Case(false, "/AutomatorDocument.icns", "rdoc", 9, 13,
                ScoreMatch * 4 + BonusCamel123 + BonusConsecutive * 2);
            yield return Case(false, "/man1/zshcompctl.1", "zshc", 6, 10,
                ScoreMatch * 4 + BBD * BonusFirstCharMultiplier + BBD * 3);
            yield return Case(false, "/.oh-my-zsh/cache", "zshc", 8, 13,
                ScoreMatch * 4 + BonusBoundary * BonusFirstCharMultiplier + BonusBoundary * 2 + ScoreGapStart + BBD);
            // 先頭の記号は強い境界として扱われる
            yield return Case(false, ".vimrc", ".vimrc", 0, 6,
                ScoreMatch * 6 + BBW * (BonusFirstCharMultiplier + 5));
            // 区切り文字直後の記号は区切りの境界ボーナスを引き継ぐ
            yield return Case(false, "/.vimrc", ".vimrc", 1, 7,
                ScoreMatch * 6 + BBD * (BonusFirstCharMultiplier + 5));
            // 語の途中の記号は bonusNonWord のまま
            yield return Case(false, "a.vimrc", ".vimrc", 1, 7,
                ScoreMatch * 6 + BonusBoundary * (BonusFirstCharMultiplier + 5));
            yield return Case(false, "ab0123 456", "12356", 3, 10,
                ScoreMatch * 5 + BonusConsecutive * 3 + ScoreGapStart + ScoreGapExtension);
            yield return Case(false, "abc123 456", "12356", 3, 10,
                ScoreMatch * 5 + BonusCamel123 * BonusFirstCharMultiplier + BonusCamel123 * 2 + BonusConsecutive + ScoreGapStart + ScoreGapExtension);
            yield return Case(false, "foo/bar/baz", "fbb", 0, 9,
                ScoreMatch * 3 + BBW * BonusFirstCharMultiplier + BBD * 2 + 2 * ScoreGapStart + 4 * ScoreGapExtension);
            yield return Case(false, "fooBarBaz", "fbb", 0, 7,
                ScoreMatch * 3 + BBW * BonusFirstCharMultiplier + BonusCamel123 * 2 + 2 * ScoreGapStart + 2 * ScoreGapExtension);
            yield return Case(false, "foo barbaz", "fbb", 0, 8,
                ScoreMatch * 3 + BBW * BonusFirstCharMultiplier + BBW + ScoreGapStart * 2 + ScoreGapExtension * 3);
            yield return Case(false, "fooBar Baz", "foob", 0, 4,
                ScoreMatch * 4 + BBW * BonusFirstCharMultiplier + BBW * 3);
            yield return Case(false, "xFoo-Bar Baz", "foo-b", 1, 6,
                ScoreMatch * 5 + BonusCamel123 * BonusFirstCharMultiplier + BonusCamel123 * 2 + BonusNonWord + BonusBoundary);

            yield return Case(true, "fooBarbaz", "oBz", 2, 9,
                ScoreMatch * 3 + BonusCamel123 + ScoreGapStart + ScoreGapExtension * 3);
            yield return Case(true, "Foo/Bar/Baz", "FBB", 0, 9,
                ScoreMatch * 3 + BBW * BonusFirstCharMultiplier + BBD * 2 + ScoreGapStart * 2 + ScoreGapExtension * 4);
            yield return Case(true, "FooBarBaz", "FBB", 0, 7,
                ScoreMatch * 3 + BBW * BonusFirstCharMultiplier + BonusCamel123 * 2 + ScoreGapStart * 2 + ScoreGapExtension * 2);
            yield return Case(true, "FooBar Baz", "FooB", 0, 4,
                ScoreMatch * 4 + BBW * BonusFirstCharMultiplier + BBW * 2 + Math.Max(BonusCamel123, BBW));
            // 連続ボーナスが更新されるケース
            yield return Case(true, "foo-bar", "o-ba", 2, 6,
                ScoreMatch * 4 + BonusBoundary * 3);

            static object[] Case(bool caseSensitive, string input, string pattern, int start, int end, int score) =>
                new object[] { caseSensitive, input, pattern, start, end, score };
        }

        [Theory]
        [MemberData(nameof(FuzzyMatchCases))]
        public void Match_ReproducesFzfScoresAndPositions_Forward(bool caseSensitive, string input, string pattern, int start, int end, int score)
        {
            AssertMatch(caseSensitive, forward: true, input, pattern, start, end, score);
        }

        [Theory]
        [MemberData(nameof(FuzzyMatchCases))]
        public void Match_ReproducesFzfScoresAndPositions_Backward(bool caseSensitive, string input, string pattern, int start, int end, int score)
        {
            AssertMatch(caseSensitive, forward: false, input, pattern, start, end, score);
        }

        [Theory]
        [InlineData("fooBarbaz", "oBZ")]
        [InlineData("Foo Bar Baz", "fbb")]
        [InlineData("fooBarbaz", "fooBarbazz")]
        public void Match_CaseSensitiveMismatch_ReturnsNull(string input, string pattern)
        {
            Assert.Null(Match(input, pattern, caseSensitive: true, forward: true));
            Assert.Null(Match(input, pattern, caseSensitive: true, forward: false));
        }

        [Fact]
        public void Match_EmptyPattern_ReturnsZeroScoreMatch()
        {
            var match = Match("foobar", "", caseSensitive: false);

            Assert.NotNull(match);
            Assert.Equal((0, 0, 0), (match!.Start, match.End, match.Score));
            Assert.Empty(match.Positions);
        }

        [Fact]
        public void Match_LongString_FindsSingleCharacterAtEnd()
        {
            var input = new string('x', 1000) + "y";

            var match = Match(input, "y", caseSensitive: false);

            Assert.NotNull(match);
            Assert.Equal(new[] { 1000 }, match!.Positions);
            Assert.Equal(ScoreMatch, match.Score);
        }

        [Fact]
        public void Match_ForwardAndBackward_PickDifferentOccurrencesOnTie()
        {
            var forward = Match("ab ab", "ab", caseSensitive: false, forward: true)!;
            var backward = Match("ab ab", "ab", caseSensitive: false, forward: false)!;

            Assert.Equal(new[] { 0, 1 }, forward.Positions);
            Assert.Equal(new[] { 3, 4 }, backward.Positions);
            Assert.Equal(forward.Score, backward.Score);
        }

        [Fact]
        public void Match_PrefersWordBoundaryOverEarlierLooseMatch()
        {
            // "a_____b__c__abc" の例: 末尾の連続一致が選ばれる
            var match = Match("a_____b__c__abc", "abc", caseSensitive: false, forward: true)!;

            Assert.Equal(new[] { 12, 13, 14 }, match.Positions);
        }

        [Fact]
        public void Match_NonAsciiLetters_AreMatchedByCharacter()
        {
            var match = Match("メモ帳", "メモ", caseSensitive: false)!;

            Assert.Equal(new[] { 0, 1 }, match.Positions);
            Assert.Equal(ScoreMatch * 2 + BBW * BonusFirstCharMultiplier + BBW, match.Score);
        }

        [Fact]
        public void Match_PathScheme_TreatsBackslashAsDelimiter()
        {
            var path = @"C:\Users\me\notes.txt";

            var withPath = Match(path, "notes", caseSensitive: false, forward: false, FzfScheme.Path)!;
            var withDefault = Match(path, "notes", caseSensitive: false, forward: false, FzfScheme.Default)!;

            Assert.Equal(Enumerable.Range(12, 5), withPath.Positions);
            // path スキームでは "\" 直後が語頭扱いになり、default スキームより高く評価される
            Assert.True(withPath.Score > withDefault.Score);
        }

        [Fact]
        public void IsCaseSensitive_FollowsSmartCase()
        {
            Assert.False(IsCaseSensitive("foo"));
            Assert.False(IsCaseSensitive("foo bar 123"));
            Assert.True(IsCaseSensitive("Foo"));
            Assert.True(IsCaseSensitive("fooB"));
        }

        private static void AssertMatch(bool caseSensitive, bool forward, string input, string pattern, int start, int end, int score)
        {
            var match = Match(input, pattern, caseSensitive, forward);

            Assert.NotNull(match);
            var positions = match!.Positions.OrderBy(p => p).ToList();
            Assert.Equal(start, positions[0]);
            Assert.Equal(end, positions[^1] + 1);
            Assert.Equal(start, match.Start);
            Assert.Equal(end, match.End);
            Assert.Equal(score, match.Score);
            Assert.Equal(pattern.Length, positions.Count);
        }
    }
}
