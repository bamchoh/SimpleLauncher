namespace SimpleLauncher.Controls
{
    public sealed record TextSegment(string Text, bool IsHighlighted);

    /// <summary>文字列を、強調表示する区間とそれ以外の区間に分割する。</summary>
    public static class HighlightSplitter
    {
        public static IReadOnlyList<TextSegment> Split(string? text, IEnumerable<int>? highlightPositions)
        {
            if (string.IsNullOrEmpty(text))
            {
                return Array.Empty<TextSegment>();
            }

            var highlighted = highlightPositions == null
                ? new HashSet<int>()
                : new HashSet<int>(highlightPositions);

            var segments = new List<TextSegment>();
            var pos = 0;

            while (pos < text.Length)
            {
                var isHighlighted = highlighted.Contains(pos);
                var start = pos;
                pos++;

                // 同じ種類 (強調 / 通常) が続く間まとめる
                while (pos < text.Length && highlighted.Contains(pos) == isHighlighted)
                {
                    pos++;
                }

                segments.Add(new TextSegment(text[start..pos], isHighlighted));
            }

            return segments;
        }
    }
}
