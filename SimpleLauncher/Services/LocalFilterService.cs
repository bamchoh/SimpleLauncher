using SimpleLauncher.Models;
using SimpleLauncher.Services.Fuzzy;

namespace SimpleLauncher.Services
{
    /// <summary>
    /// プロセス内で fzf のアルゴリズムを使ってアプリ一覧とコマンド一覧を絞り込む。
    /// </summary>
    public sealed class LocalFilterService : IFilterService
    {
        private IReadOnlyList<string> _apps = Array.Empty<string>();
        private IReadOnlyList<string> _commands = Array.Empty<string>();

        public Task SetListAsync(IReadOnlyList<string> items)
        {
            _apps = items.ToArray();
            return Task.CompletedTask;
        }

        public Task SetCommandListAsync(IReadOnlyList<string> items)
        {
            _commands = items.ToArray();
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<FilterResult>> FilterAsync(string pattern)
        {
            var results = FuzzyFilter.Filter(ItemKind.App, _apps, pattern);
            results.AddRange(FuzzyFilter.Filter(ItemKind.Command, _commands, pattern));
            FilterResultSorter.SortByScore(results);

            return Task.FromResult<IReadOnlyList<FilterResult>>(results);
        }
    }
}
