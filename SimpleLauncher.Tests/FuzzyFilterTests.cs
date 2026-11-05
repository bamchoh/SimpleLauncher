using SimpleLauncher.Models;
using SimpleLauncher.Services;
using SimpleLauncher.Services.Fuzzy;

namespace SimpleLauncher.Tests
{
    public class FuzzyFilterTests
    {
        [Fact]
        public void Filter_EmptyPattern_ReturnsAllUnmatchedInOrder()
        {
            var results = FuzzyFilter.Filter(ItemKind.File, new[] { "b", "a" }, "");

            Assert.Equal(new[] { "b", "a" }, results.Select(r => r.Text));
            Assert.All(results, r => Assert.Equal(new[] { -1 }, r.Pos));
        }

        [Fact]
        public void Filter_DropsNonMatchesAndSortsByScore()
        {
            var items = new[] { "Visual Studio Code", "vscode", "notepad", "Volume Settings Config" };

            var results = FuzzyFilter.Filter(ItemKind.App, items, "vsc");

            Assert.DoesNotContain("notepad", results.Select(r => r.Text));
            Assert.Equal(3, results.Count);
            Assert.True(results[0].Score >= results[1].Score && results[1].Score >= results[2].Score);
            Assert.All(results, r => Assert.Equal(ItemKind.App, r.Type));
        }

        [Fact]
        public void Filter_RecordsMatchedPositionsForHighlighting()
        {
            var result = Assert.Single(FuzzyFilter.Filter(ItemKind.App, new[] { "Google Chrome" }, "gc"));

            Assert.Equal(new[] { 0, 7 }, result.Pos);
        }

        [Fact]
        public void Filter_SmartCase_UppercasePatternIsCaseSensitive()
        {
            var items = new[] { "Notepad", "notepad" };

            Assert.Equal(2, FuzzyFilter.Filter(ItemKind.App, items, "note").Count);
            Assert.Equal("Notepad", Assert.Single(FuzzyFilter.Filter(ItemKind.App, items, "Note")).Text);
        }

        [Fact]
        public void Filter_SpacesArePartOfThePattern()
        {
            var items = new[] { "power shell", "powershell" };

            var results = FuzzyFilter.Filter(ItemKind.Command, items, "r s");

            Assert.Equal("power shell", Assert.Single(results).Text);
        }

        [Fact]
        public async Task LocalFilterService_FiltersBothListsWithTheirTypes()
        {
            var service = new LocalFilterService();
            await service.SetListAsync(new[] { "Google Chrome", "Notepad" });
            await service.SetCommandListAsync(new[] { "google.com", "--edit" });

            var results = await service.FilterAsync("goo");

            Assert.Equal(2, results.Count);
            Assert.Contains(results, r => r.Text == "Google Chrome" && r.Type == ItemKind.App);
            Assert.Contains(results, r => r.Text == "google.com" && r.Type == ItemKind.Command);
            Assert.All(results, r => Assert.True(r.Score > 0));
        }

        [Fact]
        public async Task LocalFilterService_EmptyPattern_ReturnsEverything()
        {
            var service = new LocalFilterService();
            await service.SetListAsync(new[] { "a" });
            await service.SetCommandListAsync(new[] { "b" });

            var results = await service.FilterAsync("");

            Assert.Equal(2, results.Count);
        }
    }
}
