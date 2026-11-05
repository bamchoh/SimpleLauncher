using SimpleLauncher.Models;
using SimpleLauncher.Services;

namespace SimpleLauncher.Tests
{
    public class LocalFuzzyMatcherTests
    {
        [Fact]
        public void Match_Substring_ScoresMaxAndHighlightsLastOccurrence()
        {
            var result = LocalFuzzyMatcher.Match(ItemKind.File, @"C:\Documents\report.docx", "doc");

            Assert.NotNull(result);
            Assert.Equal(LocalFuzzyMatcher.MaxScore, result!.Score);
            var start = @"C:\Documents\report.docx".LastIndexOf("doc", StringComparison.OrdinalIgnoreCase);
            Assert.Equal(new[] { start, start + 1, start + 2 }, result.Pos);
        }

        [Fact]
        public void Match_IsCaseInsensitive()
        {
            Assert.NotNull(LocalFuzzyMatcher.Match(ItemKind.File, "README.md", "readme"));
            Assert.NotNull(LocalFuzzyMatcher.Match(ItemKind.File, "readme.md", "RDM"));
        }

        [Fact]
        public void Match_Subsequence_ScoresBelowMaxAndRecordsPositions()
        {
            var result = LocalFuzzyMatcher.Match(ItemKind.File, "notepad.exe", "npd");

            Assert.NotNull(result);
            Assert.InRange(result!.Score, 1, LocalFuzzyMatcher.MaxScore - 1);
            Assert.Equal(new[] { 0, 4, 6 }, result.Pos);
        }

        [Fact]
        public void Match_NoMatch_ReturnsNull()
        {
            Assert.Null(LocalFuzzyMatcher.Match(ItemKind.File, "notepad.exe", "xyz"));
            Assert.Null(LocalFuzzyMatcher.Match(ItemKind.File, "abc", "abcd"));
        }

        [Fact]
        public void Match_MultipleTerms_AllMustMatch()
        {
            Assert.NotNull(LocalFuzzyMatcher.Match(ItemKind.File, @"C:\work\notes.txt", "work txt"));
            Assert.Null(LocalFuzzyMatcher.Match(ItemKind.File, @"C:\work\notes.txt", "work pdf"));
        }

        [Fact]
        public void Match_TighterSubsequence_ScoresHigher()
        {
            var tight = LocalFuzzyMatcher.Match(ItemKind.File, "abc", "ac")!;
            var loose = LocalFuzzyMatcher.Match(ItemKind.File, "a-------c", "ac")!;

            Assert.True(tight.Score > loose.Score);
        }

        [Fact]
        public void Filter_EmptyPattern_ReturnsAllUnmatched()
        {
            var results = LocalFuzzyMatcher.Filter(ItemKind.File, new[] { "b", "a" }, "   ");

            Assert.Equal(new[] { "b", "a" }, results.Select(r => r.Text));
            Assert.All(results, r => Assert.Equal(new[] { -1 }, r.Pos));
        }

        [Fact]
        public void Filter_SortsByScoreThenLength()
        {
            var items = new[] { "zz-report-long.docx", "report.docx", "r-e-p-o-r-t" };

            var results = LocalFuzzyMatcher.Filter(ItemKind.File, items, "report");

            Assert.Equal(new[] { "report.docx", "zz-report-long.docx", "r-e-p-o-r-t" }, results.Select(r => r.Text));
            Assert.All(results, r => Assert.Equal(ItemKind.File, r.Type));
        }
    }
}
