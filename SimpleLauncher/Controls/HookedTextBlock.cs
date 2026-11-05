using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using SimpleLauncher.Models;

namespace SimpleLauncher.Controls
{
    /// <summary>
    /// DataContext の <see cref="FilterResult.Pos"/> に基づいて、一致した文字を強調表示する TextBlock。
    /// </summary>
    public class HookedTextBlock : TextBlock
    {
        private static readonly Brush HighlightBrush = CreateFrozenBrush(Colors.Red);

        static HookedTextBlock()
        {
            TextProperty.OverrideMetadata(
                typeof(HookedTextBlock),
                new FrameworkPropertyMetadata(string.Empty, (d, _) => ((HookedTextBlock)d).Rebuild()));
        }

        public HookedTextBlock()
        {
            // 仮想化で同じコンテナが使い回され Text が変わらない場合に備える
            DataContextChanged += (_, _) => Rebuild();
        }

        private void Rebuild()
        {
            var positions = (DataContext as FilterResult)?.Pos;
            var segments = HighlightSplitter.Split(Text, positions);

            Inlines.Clear();

            foreach (var segment in segments)
            {
                var run = new Run(segment.Text);
                if (segment.IsHighlighted)
                {
                    run.FontWeight = FontWeights.Bold;
                    run.Foreground = HighlightBrush;
                }

                Inlines.Add(run);
            }
        }

        private static Brush CreateFrozenBrush(Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }
    }
}
