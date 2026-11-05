using SimpleLauncher.Controls;

namespace SimpleLauncher.Tests
{
    public class HighlightSplitterTests
    {
        [Fact]
        public void Split_EmptyText_ReturnsNoSegments()
        {
            Assert.Empty(HighlightSplitter.Split("", new[] { 0 }));
            Assert.Empty(HighlightSplitter.Split(null, null));
        }

        [Fact]
        public void Split_NoHighlights_ReturnsSingleSegment()
        {
            var segments = HighlightSplitter.Split("hello", new[] { -1 });

            var segment = Assert.Single(segments);
            Assert.Equal("hello", segment.Text);
            Assert.False(segment.IsHighlighted);
        }

        [Fact]
        public void Split_NullPositions_TreatedAsNoHighlights()
        {
            var segment = Assert.Single(HighlightSplitter.Split("abc", null));

            Assert.False(segment.IsHighlighted);
        }

        [Fact]
        public void Split_GroupsConsecutiveHighlightedCharacters()
        {
            var segments = HighlightSplitter.Split("notepad", new[] { 0, 1, 4 });

            Assert.Equal(
                new[] { ("no", true), ("te", false), ("p", true), ("ad", false) },
                segments.Select(s => (s.Text, s.IsHighlighted)).ToArray());
        }

        [Fact]
        public void Split_HighlightAtEnd_IsIncluded()
        {
            var segments = HighlightSplitter.Split("ab", new[] { 1 });

            Assert.Equal(
                new[] { ("a", false), ("b", true) },
                segments.Select(s => (s.Text, s.IsHighlighted)).ToArray());
        }

        [Fact]
        public void Split_AllHighlighted_ReturnsSingleHighlightedSegment()
        {
            var segment = Assert.Single(HighlightSplitter.Split("abc", new[] { 0, 1, 2 }));

            Assert.Equal("abc", segment.Text);
            Assert.True(segment.IsHighlighted);
        }

        [Fact]
        public void Split_SegmentsConcatenateToOriginalText()
        {
            const string text = "C:\\Program Files\\App\\app.exe";

            var segments = HighlightSplitter.Split(text, new[] { 3, 4, 11, 20, 21, 22 });

            Assert.Equal(text, string.Concat(segments.Select(s => s.Text)));
        }
    }
}
