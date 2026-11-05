using SimpleLauncher.Config;
using SimpleLauncher.Models;
using SimpleLauncher.Services;
using SimpleLauncher.ViewModels;

namespace SimpleLauncher.Tests
{
    public class MainWindowVMTests
    {
        private readonly FakeFilterService _filter = new();
        private readonly FakeInstalledAppProvider _apps = new();
        private readonly FakeConfigProvider _config = new();
        private readonly FakeRecentFilesProvider _recent = new();
        private readonly FakeProcessLauncher _processes = new();
        private readonly FakeShortcutResolver _resolver = new();
        private readonly FakeErrorReporter _errors = new();
        private readonly MainWindowVM _vm;

        public MainWindowVMTests()
        {
            _apps.Apps["Zed"] = new LaunchableItem("Zed", @"C:\menu\Zed.lnk");
            _apps.Apps["Editor"] = new LaunchableItem("Editor", @"C:\menu\Editor.lnk");
            _config.Config = LauncherConfigLoader.Parse("list:\n  - |-\n    shell\n    powershell\n", "conf.yaml");
            _recent.Files.AddRange(new[] { @"C:\docs\report.docx", @"C:\docs\notes.txt" });

            _vm = new MainWindowVM(
                _filter,
                _apps,
                _config,
                _recent,
                _processes,
                new LaunchPlanner(_resolver, _ => null),
                _errors);
        }

        [Fact]
        public async Task InitializeAsync_LoadsSortedItemsAndPushesListsToFilterService()
        {
            await _vm.InitializeAsync();

            Assert.Equal(new[] { "--edit", "Editor", "shell", "Zed" }, _vm.FilteredResult.Select(r => r.Text));
            Assert.Equal(ItemKind.Command, _vm.FilteredResult.Single(r => r.Text == "shell").Type);
            Assert.Equal(new[] { "Zed", "Editor" }, _filter.Items);
            Assert.Equal(new[] { "--edit", "shell" }, _filter.Commands);
            Assert.Same(_vm.FilteredResult[0], _vm.SelectedItem);
            Assert.Empty(_errors.Errors);
        }

        [Fact]
        public async Task InitializeAsync_LoadsRecentFilesInGivenOrder()
        {
            await _vm.InitializeAsync();

            Assert.Equal(new[] { @"C:\docs\report.docx", @"C:\docs\notes.txt" }, _vm.RecentFiles.Select(r => r.Text));
            Assert.All(_vm.RecentFiles, r => Assert.Equal(ItemKind.File, r.Type));
            Assert.Same(_vm.RecentFiles[0], _vm.SelectedRecentItem);
        }

        [Fact]
        public async Task InitializeAsync_WhenScanFails_ReportsErrorInsteadOfThrowing()
        {
            _apps.Failure = new InvalidOperationException("scan failed");

            await _vm.InitializeAsync();

            Assert.Equal(new[] { "scan failed" }, _errors.Errors);
            Assert.Empty(_vm.FilteredResult);
        }

        [Fact]
        public async Task Pattern_FiltersThroughServiceAndSortsByScore()
        {
            await _vm.InitializeAsync();
            _filter.OnFilter = _ => Task.FromResult<IReadOnlyList<FilterResult>>(new[]
            {
                Items.App("Editor", 10),
                Items.Command("shell", 90),
            });

            _vm.Pattern = "e";
            await _vm.LastFilterTask;

            Assert.Equal(new[] { "e" }, _filter.Patterns);
            Assert.Equal(new[] { "shell", "Editor" }, _vm.FilteredResult.Select(r => r.Text));
            Assert.Equal("shell", _vm.SelectedItem?.Text);
        }

        [Fact]
        public async Task Pattern_Cleared_RestoresFullListWithoutCallingService()
        {
            await _vm.InitializeAsync();
            _filter.OnFilter = _ => Task.FromResult<IReadOnlyList<FilterResult>>(new[] { Items.App("Zed") });
            _vm.Pattern = "z";
            await _vm.LastFilterTask;

            _vm.Pattern = "";
            await _vm.LastFilterTask;

            Assert.Equal(4, _vm.FilteredResult.Count);
            Assert.Single(_filter.Patterns);
        }

        [Fact]
        public async Task Pattern_StaleResultArrivingLate_IsIgnored()
        {
            await _vm.InitializeAsync();
            var slow = new TaskCompletionSource<IReadOnlyList<FilterResult>>();
            var fast = new TaskCompletionSource<IReadOnlyList<FilterResult>>();
            _filter.OnFilter = pattern => pattern == "e" ? slow.Task : fast.Task;

            _vm.Pattern = "e";
            var slowTask = _vm.LastFilterTask;
            _vm.Pattern = "ed";
            var fastTask = _vm.LastFilterTask;

            fast.SetResult(new[] { Items.App("Editor") });
            await fastTask;
            slow.SetResult(new[] { Items.App("Editor"), Items.Command("shell") });
            await slowTask;

            Assert.Equal(new[] { "Editor" }, _vm.FilteredResult.Select(r => r.Text));
        }

        [Fact]
        public async Task Pattern_WhenServiceFails_KeepsPreviousResults()
        {
            await _vm.InitializeAsync();
            _filter.OnFilter = _ => Task.FromException<IReadOnlyList<FilterResult>>(new IOException("pipe broken"));

            _vm.Pattern = "x";
            await _vm.LastFilterTask;

            Assert.Equal(4, _vm.FilteredResult.Count);
            Assert.Empty(_errors.Errors);
        }

        [Fact]
        public async Task ReloadAsync_ReappliesCurrentPattern()
        {
            await _vm.InitializeAsync();
            _filter.OnFilter = _ => Task.FromResult<IReadOnlyList<FilterResult>>(new[] { Items.App("Zed") });
            _vm.Pattern = "z";
            await _vm.LastFilterTask;
            _apps.Apps["Zoo"] = new LaunchableItem("Zoo", @"C:\menu\Zoo.lnk");

            await _vm.ReloadAsync();

            Assert.Equal(new[] { "z", "z" }, _filter.Patterns);
            Assert.Contains("Zoo", _filter.Items);
            Assert.Equal(new[] { "Zed" }, _vm.FilteredResult.Select(r => r.Text));
        }

        [Fact]
        public async Task RecentPattern_FiltersLocally()
        {
            await _vm.InitializeAsync();

            _vm.RecentPattern = "notes";

            Assert.Equal(new[] { @"C:\docs\notes.txt" }, _vm.RecentFiles.Select(r => r.Text));
            Assert.Equal(@"C:\docs\notes.txt", _vm.SelectedRecentItem?.Text);

            _vm.RecentPattern = "";

            Assert.Equal(2, _vm.RecentFiles.Count);
        }

        [Fact]
        public async Task MoveSelection_StopsAtBounds()
        {
            await _vm.InitializeAsync();

            _vm.MoveSelection(-1);
            Assert.Equal("--edit", _vm.SelectedItem?.Text);

            _vm.MoveSelection(+2);
            Assert.Equal("shell", _vm.SelectedItem?.Text);

            _vm.MoveSelection(+10);
            Assert.Equal("Zed", _vm.SelectedItem?.Text);
        }

        [Fact]
        public async Task MoveSelection_OnRecentTab_MovesRecentSelection()
        {
            await _vm.InitializeAsync();
            _vm.SelectedTabIndex = MainWindowVM.RecentTabIndex;

            _vm.MoveSelection(+1);

            Assert.Equal(@"C:\docs\notes.txt", _vm.SelectedRecentItem?.Text);
            Assert.Equal("--edit", _vm.SelectedItem?.Text);
        }

        [Fact]
        public void MoveSelection_EmptyList_DoesNothing()
        {
            _vm.MoveSelection(+1);

            Assert.Null(_vm.SelectedItem);
        }

        [Fact]
        public void SwitchTab_WrapsAround()
        {
            Assert.True(_vm.IsAppsTabActive);

            _vm.SwitchTab(+1);
            Assert.Equal(MainWindowVM.RecentTabIndex, _vm.SelectedTabIndex);
            Assert.False(_vm.IsAppsTabActive);

            _vm.SwitchTab(+1);
            Assert.Equal(MainWindowVM.AppsTabIndex, _vm.SelectedTabIndex);

            _vm.SwitchTab(-1);
            Assert.Equal(MainWindowVM.RecentTabIndex, _vm.SelectedTabIndex);
        }

        [Fact]
        public async Task ExecuteSelected_OnAppsTab_LaunchesSelectedApp()
        {
            await _vm.InitializeAsync();
            _vm.SelectedItem = _vm.FilteredResult.Single(r => r.Text == "Editor");

            var outcome = _vm.ExecuteSelected("runas");

            Assert.Equal(LaunchOutcome.Launched, outcome);
            var psi = Assert.Single(_processes.Started);
            Assert.Equal(@"C:\menu\Editor.lnk", psi.FileName);
            Assert.Equal("runas", psi.Verb);
        }

        [Fact]
        public async Task ExecuteSelected_OnRecentTab_LaunchesSelectedFile()
        {
            await _vm.InitializeAsync();
            _vm.SelectedTabIndex = MainWindowVM.RecentTabIndex;

            var outcome = _vm.ExecuteSelected();

            Assert.Equal(LaunchOutcome.Launched, outcome);
            Assert.Equal(@"C:\docs\report.docx", Assert.Single(_processes.Started).FileName);
        }

        [Fact]
        public void ExecuteSelected_NothingSelected_DoesNotLaunch()
        {
            var outcome = _vm.ExecuteSelected();

            Assert.Equal(LaunchOutcome.NothingSelected, outcome);
            Assert.Empty(_processes.Started);
        }

        [Fact]
        public async Task ExecuteSelected_WhenLaunchFails_ReportsError()
        {
            await _vm.InitializeAsync();
            _processes.Failure = new System.ComponentModel.Win32Exception("cancelled by user");

            var outcome = _vm.ExecuteSelected();

            Assert.Equal(LaunchOutcome.Failed, outcome);
            Assert.Contains("cancelled by user", Assert.Single(_errors.Errors));
        }

        [Fact]
        public async Task OpenSelectedLocation_UsesExplorer()
        {
            await _vm.InitializeAsync();
            _resolver.Targets[@"C:\menu\Editor.lnk"] = @"C:\Editor\editor.exe";
            _vm.SelectedItem = _vm.FilteredResult.Single(r => r.Text == "Editor");

            var outcome = _vm.OpenSelectedLocation();

            Assert.Equal(LaunchOutcome.Launched, outcome);
            var psi = Assert.Single(_processes.Started);
            Assert.Equal("explorer.exe", psi.FileName);
            Assert.Contains(@"C:\Editor\editor.exe", psi.Arguments);
        }

        [Fact]
        public async Task OpenSelectedLocation_ForCommandWithoutLocation_ReportsError()
        {
            await _vm.InitializeAsync();
            _vm.SelectedItem = _vm.FilteredResult.Single(r => r.Text == "shell");

            var outcome = _vm.OpenSelectedLocation();

            Assert.Equal(LaunchOutcome.Failed, outcome);
            Assert.Single(_errors.Errors);
            Assert.Empty(_processes.Started);
        }
    }
}
