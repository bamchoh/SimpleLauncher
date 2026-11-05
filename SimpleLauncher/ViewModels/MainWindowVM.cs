using System.Diagnostics;
using Prism.Mvvm;
using SimpleLauncher.Config;
using SimpleLauncher.Models;
using SimpleLauncher.Services;
using SimpleLauncher.Services.Fuzzy;

namespace SimpleLauncher.ViewModels
{
    /// <summary><see cref="MainWindowVM.RunAction"/> 等の結果。</summary>
    public enum LaunchOutcome
    {
        /// <summary>選択されている項目が無い。</summary>
        NothingSelected,

        /// <summary>起動した。</summary>
        Launched,

        /// <summary>失敗し、エラーをユーザーに表示した。</summary>
        Failed,
    }

    /// <summary>アクションメニューの 1 行。</summary>
    /// <param name="Action">実行するアクション。</param>
    /// <param name="Match">名前と強調位置。</param>
    public sealed record ActionMenuItem(LauncherAction Action, FilterResult Match)
    {
        public string KeyText => Action.Key ?? string.Empty;
    }

    public sealed class MainWindowVM : BindableBase
    {
        public const int AppsTabIndex = 0;
        public const int RecentTabIndex = 1;
        public const int TabCount = 2;

        private const string ActionItemKind = "action";

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

        private IReadOnlyList<LauncherAction> _actions = LauncherAction.Defaults;
        private bool _isActionMenuOpen;
        private FilterResult? _actionMenuTarget;
        private string _actionPattern = string.Empty;
        private IReadOnlyList<ActionMenuItem> _actionMenuItems = Array.Empty<ActionMenuItem>();
        private ActionMenuItem? _selectedActionMenuItem;

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

        // ---- アクション ----

        /// <summary>利用できるアクション。設定の読み込みで更新される。</summary>
        public IReadOnlyList<LauncherAction> Actions
        {
            get => _actions;
            private set => SetProperty(ref _actions, value);
        }

        public bool IsActionMenuOpen
        {
            get => _isActionMenuOpen;
            private set => SetProperty(ref _isActionMenuOpen, value);
        }

        /// <summary>アクションメニューの対象の項目。</summary>
        public FilterResult? ActionMenuTarget
        {
            get => _actionMenuTarget;
            private set => SetProperty(ref _actionMenuTarget, value);
        }

        public string ActionPattern
        {
            get => _actionPattern;
            set
            {
                if (SetProperty(ref _actionPattern, value ?? string.Empty))
                {
                    ApplyActionPattern(_actionPattern);
                }
            }
        }

        public IReadOnlyList<ActionMenuItem> ActionMenuItems
        {
            get => _actionMenuItems;
            private set
            {
                _actionMenuItems = value;
                RaisePropertyChanged();
                SelectedActionMenuItem = value.Count > 0 ? value[0] : null;
            }
        }

        public ActionMenuItem? SelectedActionMenuItem
        {
            get => _selectedActionMenuItem;
            set => SetProperty(ref _selectedActionMenuItem, value);
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
                Actions = config.ActionList;

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

        /// <summary>
        /// 選択を delta 件分動かす。端で止まる。
        /// アクションメニューが開いていればメニュー内の選択を動かす。
        /// </summary>
        public void MoveSelection(int delta)
        {
            if (IsActionMenuOpen)
            {
                var actions = ActionMenuItems;
                if (actions.Count > 0)
                {
                    var current = IndexOf(actions, SelectedActionMenuItem);
                    SelectedActionMenuItem = actions[Math.Clamp(current + delta, 0, actions.Count - 1)];
                }

                return;
            }

            var items = ActiveItems;
            if (items.Count == 0)
            {
                return;
            }

            var index = Math.Clamp(IndexOf(items, ActiveSelectedItem) + delta, 0, items.Count - 1);

            if (IsAppsTabActive)
            {
                SelectedItem = items[index];
            }
            else
            {
                SelectedRecentItem = items[index];
            }
        }

        /// <summary>タブを delta 個分切り替える。末尾と先頭は繋がっている。</summary>
        public void SwitchTab(int delta)
        {
            SelectedTabIndex = ((SelectedTabIndex + delta) % TabCount + TabCount) % TabCount;
        }

        /// <summary>アクティブなタブで選択中の項目にアクションを適用する。</summary>
        public LaunchOutcome RunAction(LauncherAction action)
        {
            var item = ActiveSelectedItem;
            if (item == null)
            {
                return LaunchOutcome.NothingSelected;
            }

            try
            {
                _processLauncher.Start(_planner.CreateActionInfo(action, item, _catalog));
                return LaunchOutcome.Launched;
            }
            catch (Exception ex)
            {
                _errors.ShowError(ex.Message);
                return LaunchOutcome.Failed;
            }
        }

        /// <summary>選択中の項目に対するアクションメニューを開く。</summary>
        /// <returns>項目が選択されていなければ false。</returns>
        public bool OpenActionMenu()
        {
            var item = ActiveSelectedItem;
            if (item == null)
            {
                return false;
            }

            ActionMenuTarget = item;
            ActionPattern = string.Empty;
            ApplyActionPattern(string.Empty);
            IsActionMenuOpen = true;
            return true;
        }

        public void CloseActionMenu()
        {
            IsActionMenuOpen = false;
            ActionMenuTarget = null;
        }

        /// <summary>メニューで選択中のアクションを実行し、メニューを閉じる。</summary>
        public LaunchOutcome RunSelectedAction()
        {
            var selected = SelectedActionMenuItem;
            CloseActionMenu();

            return selected == null ? LaunchOutcome.NothingSelected : RunAction(selected.Action);
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
                : FuzzyFilter.Filter(ItemKind.File, _allRecentFiles.Select(r => r.Text), pattern, FzfScheme.Path);
        }

        private void ApplyActionPattern(string pattern)
        {
            var byName = Actions.ToDictionary(a => a.Name, a => a);

            ActionMenuItems = FuzzyFilter.Filter(ActionItemKind, byName.Keys, pattern)
                .Select(match => new ActionMenuItem(byName[match.Text], match))
                .ToList();
        }

        private static int IndexOf<T>(IReadOnlyList<T> items, T? item) where T : class
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
