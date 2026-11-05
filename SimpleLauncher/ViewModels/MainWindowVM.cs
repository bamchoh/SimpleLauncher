using System.Diagnostics;
using Prism.Mvvm;
using SimpleLauncher.Models;
using SimpleLauncher.Services;

namespace SimpleLauncher.ViewModels
{
    /// <summary><see cref="MainWindowVM.ExecuteSelected"/> 等の結果。</summary>
    public enum LaunchOutcome
    {
        /// <summary>選択されている項目が無い。</summary>
        NothingSelected,

        /// <summary>起動した。</summary>
        Launched,

        /// <summary>失敗し、エラーをユーザーに表示した。</summary>
        Failed,
    }

    public sealed class MainWindowVM : BindableBase
    {
        public const int AppsTabIndex = 0;
        public const int RecentTabIndex = 1;
        public const int TabCount = 2;

        private readonly IFilterService _filter;
        private readonly IInstalledAppProvider _apps;
        private readonly ILauncherConfigProvider _config;
        private readonly IRecentFilesProvider _recent;
        private readonly IProcessLauncher _processLauncher;
        private readonly LaunchPlanner _planner;
        private readonly IErrorReporter _errors;

        private IReadOnlyList<FilterResult> _allItems = Array.Empty<FilterResult>();
        private IReadOnlyList<FilterResult> _allRecentFiles = Array.Empty<FilterResult>();
        private LaunchCatalog _catalog = LaunchCatalog.Empty;
        private int _filterVersion;

        private int _selectedTabIndex = AppsTabIndex;
        private string _pattern = string.Empty;
        private IReadOnlyList<FilterResult> _filteredResult = Array.Empty<FilterResult>();
        private FilterResult? _selectedItem;
        private string _recentPattern = string.Empty;
        private IReadOnlyList<FilterResult> _recentFiles = Array.Empty<FilterResult>();
        private FilterResult? _selectedRecentItem;

        public MainWindowVM(
            IFilterService filter,
            IInstalledAppProvider apps,
            ILauncherConfigProvider config,
            IRecentFilesProvider recent,
            IProcessLauncher processLauncher,
            LaunchPlanner planner,
            IErrorReporter errors)
        {
            _filter = filter;
            _apps = apps;
            _config = config;
            _recent = recent;
            _processLauncher = processLauncher;
            _planner = planner;
            _errors = errors;
        }

        // ---- タブ ----

        public int SelectedTabIndex
        {
            get => _selectedTabIndex;
            set
            {
                if (SetProperty(ref _selectedTabIndex, value))
                {
                    RaisePropertyChanged(nameof(IsAppsTabActive));
                }
            }
        }

        public bool IsAppsTabActive => SelectedTabIndex == AppsTabIndex;

        /// <summary>アクティブなタブで選択されている項目。</summary>
        public FilterResult? ActiveSelectedItem => IsAppsTabActive ? SelectedItem : SelectedRecentItem;

        /// <summary>アクティブなタブに表示されている項目。</summary>
        public IReadOnlyList<FilterResult> ActiveItems => IsAppsTabActive ? FilteredResult : RecentFiles;

        // ---- All タブ (アプリ + コマンド) ----

        public string Pattern
        {
            get => _pattern;
            set
            {
                if (SetProperty(ref _pattern, value ?? string.Empty))
                {
                    LastFilterTask = ApplyPatternAsync(_pattern);
                }
            }
        }

        public IReadOnlyList<FilterResult> FilteredResult
        {
            get => _filteredResult;
            private set
            {
                _filteredResult = value;
                RaisePropertyChanged();
                SelectedItem = value.Count > 0 ? value[0] : null;
            }
        }

        public FilterResult? SelectedItem
        {
            get => _selectedItem;
            set => SetProperty(ref _selectedItem, value);
        }

        /// <summary>直近の <see cref="Pattern"/> 変更で開始したフィルタ処理。テストで完了を待つために公開している。</summary>
        internal Task LastFilterTask { get; private set; } = Task.CompletedTask;

        // ---- Recent タブ (最近使ったファイル) ----

        public string RecentPattern
        {
            get => _recentPattern;
            set
            {
                if (SetProperty(ref _recentPattern, value ?? string.Empty))
                {
                    ApplyRecentPattern(_recentPattern);
                }
            }
        }

        public IReadOnlyList<FilterResult> RecentFiles
        {
            get => _recentFiles;
            private set
            {
                _recentFiles = value;
                RaisePropertyChanged();
                SelectedRecentItem = value.Count > 0 ? value[0] : null;
            }
        }

        public FilterResult? SelectedRecentItem
        {
            get => _selectedRecentItem;
            set => SetProperty(ref _selectedRecentItem, value);
        }

        // ---- 読み込み ----

        /// <summary>アプリ一覧・設定・最近使ったファイルをすべて読み込む。</summary>
        public Task InitializeAsync() => Task.WhenAll(ReloadAsync(), LoadRecentFilesAsync());

        /// <summary>アプリ一覧と設定を読み直し、現在のパターンでフィルタし直す。</summary>
        public async Task ReloadAsync()
        {
            try
            {
                var apps = await Task.Run(() => _apps.GetInstalledApps());
                var config = await Task.Run(() => _config.Load());

                var appNames = apps.Keys.ToList();
                var commandNames = config.CommandList.Keys.ToList();

                await _filter.SetListAsync(appNames);
                await _filter.SetCommandListAsync(commandNames);

                var items = appNames.Select(name => FilterResult.Unmatched(ItemKind.App, name))
                    .Concat(commandNames.Select(name => FilterResult.Unmatched(ItemKind.Command, name)))
                    .ToList();
                FilterResultSorter.SortByText(items);

                _catalog = new LaunchCatalog(apps, config);
                _allItems = items;

                await ApplyPatternAsync(Pattern);
            }
            catch (Exception ex)
            {
                _errors.ShowError(ex.Message);
            }
        }

        public async Task LoadRecentFilesAsync()
        {
            try
            {
                var files = await Task.Run(() => _recent.GetRecentFiles());

                _allRecentFiles = files.Select(path => FilterResult.Unmatched(ItemKind.File, path)).ToList();

                ApplyRecentPattern(RecentPattern);
            }
            catch (Exception ex)
            {
                _errors.ShowError(ex.Message);
            }
        }

        // ---- 操作 ----

        /// <summary>アクティブなタブの選択を delta 件分動かす。端で止まる。</summary>
        public void MoveSelection(int delta)
        {
            var items = ActiveItems;
            if (items.Count == 0)
            {
                return;
            }

            var current = IndexOf(items, ActiveSelectedItem);
            var next = Math.Clamp(current + delta, 0, items.Count - 1);

            if (IsAppsTabActive)
            {
                SelectedItem = items[next];
            }
            else
            {
                SelectedRecentItem = items[next];
            }
        }

        /// <summary>タブを delta 個分切り替える。末尾と先頭は繋がっている。</summary>
        public void SwitchTab(int delta)
        {
            SelectedTabIndex = ((SelectedTabIndex + delta) % TabCount + TabCount) % TabCount;
        }

        /// <summary>アクティブなタブで選択中の項目を起動する。</summary>
        /// <param name="verb">"open" または "runas" (管理者として実行)。</param>
        public LaunchOutcome ExecuteSelected(string verb = "open") =>
            StartSelected(item => _planner.CreateLaunchInfo(item, _catalog, verb));

        /// <summary>アクティブなタブで選択中の項目の場所をエクスプローラーで開く。</summary>
        public LaunchOutcome OpenSelectedLocation() =>
            StartSelected(item => _planner.CreateOpenLocationInfo(item, _catalog));

        private LaunchOutcome StartSelected(Func<FilterResult, ProcessStartInfo> plan)
        {
            var item = ActiveSelectedItem;
            if (item == null)
            {
                return LaunchOutcome.NothingSelected;
            }

            try
            {
                _processLauncher.Start(plan(item));
                return LaunchOutcome.Launched;
            }
            catch (Exception ex)
            {
                _errors.ShowError(ex.Message);
                return LaunchOutcome.Failed;
            }
        }

        // ---- フィルタ ----

        private async Task ApplyPatternAsync(string pattern)
        {
            // 入力中に古い結果が後から届いて上書きしないよう、最新の要求だけ反映する
            var version = Interlocked.Increment(ref _filterVersion);

            IReadOnlyList<FilterResult> results;

            if (string.IsNullOrEmpty(pattern))
            {
                results = _allItems;
            }
            else
            {
                try
                {
                    var filtered = (await _filter.FilterAsync(pattern)).ToList();
                    FilterResultSorter.SortByScore(filtered);
                    results = filtered;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Filter failed: {ex.Message}");
                    return;
                }
            }

            if (version == Volatile.Read(ref _filterVersion))
            {
                FilteredResult = results;
            }
        }

        private void ApplyRecentPattern(string pattern)
        {
            RecentFiles = string.IsNullOrWhiteSpace(pattern)
                ? _allRecentFiles
                : LocalFuzzyMatcher.Filter(ItemKind.File, _allRecentFiles.Select(r => r.Text), pattern);
        }

        private static int IndexOf(IReadOnlyList<FilterResult> items, FilterResult? item)
        {
            if (item == null)
            {
                return -1;
            }

            for (var i = 0; i < items.Count; i++)
            {
                if (ReferenceEquals(items[i], item))
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
