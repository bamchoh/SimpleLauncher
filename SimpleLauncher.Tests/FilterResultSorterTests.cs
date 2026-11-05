using SimpleLauncher.Services;

namespace SimpleLauncher.Tests
{
    public class FilterResultSorterTests
    {
        [Fact]
        public void SortByText_IsCaseInsensitiveAlphabetical()
        {
            var results = new List<Models.FilterResult> { Items.App("banana"), Items.App("Apple"), Items.App("cherry") };

            FilterResultSorter.SortByText(results);

            Assert.Equal(new[] { "Apple", "banana", "cherry" }, results.Select(r => r.Text));
        }

        [Fact]
        public void SortByScore_HighestScoreFirst()
        {
            var results = new List<Models.FilterResult> { Items.App("low", 10), Items.App("high", 90), Items.App("mid", 50) };

            FilterResultSorter.SortByScore(results);

            Assert.Equal(new[] { "high", "mid", "low" }, results.Select(r => r.Text));
        }

        [Fact]
        public void SortByScore_SameScore_ShorterTextFirst()
        {
            var results = new List<Models.FilterResult> { Items.App("longer name", 50), Items.App("short", 50) };

            FilterResultSorter.SortByScore(results);

            Assert.Equal(new[] { "short", "longer name" }, results.Select(r => r.Text));
        }

        [Fact]
        public void SortByScore_SameScoreAndLength_IsDeterministic()
        {
            var results = new List<Models.FilterResult> { Items.App("bb", 50), Items.App("aa", 50) };

            FilterResultSorter.SortByScore(results);

            Assert.Equal(new[] { "aa", "bb" }, results.Select(r => r.Text));
        }
    }
}
