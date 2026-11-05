using SimpleLauncher.Config;
using SimpleLauncher.Models;
using SimpleLauncher.Services;
using SimpleLauncher.ViewModels;

namespace SimpleLauncher.Tests
{
    public class ActionMenuTests
    {
        private readonly FakeProcessLauncher _processes = new();
        private readonly FakeErrorReporter _errors = new();
        private readonly FakeConfigProvider _config = new();
        private readonly MainWindowVM _vm;

        public ActionMenuTests()
        {
            var apps = new FakeInstalledAppProvider();
            apps.Apps["Editor"] = new LaunchableItem("Editor", @"C:\menu\Editor.lnk");

            _config.Config = LauncherConfigLoader.Parse("""
                actions:
                  - name: ターミナルで開く
                    key: Ctrl+T
                    exec: wt
                    args: -d "{dir}"
                """, "conf.yaml");

            var resolver = new FakeShortcutResolver();
            resolver.Targets[@"C:\menu\Editor.lnk"] = @"C:\Editor\editor.exe";

            _vm = new MainWindowVM(
                new FakeFilterService(),
                apps,
                _config,
                new FakeRecentFilesProvider(),
                _processes,
                new LaunchPlanner(resolver, name => name == "wt" ? @"C:\wt.exe" : null, _ => false),
                _errors);
        }

        [Fact]
        public void Actions_BeforeLoad_AreDefaults()
        {
            Assert.Equal(LauncherAction.Defaults, _vm.Actions);
        }

        [Fact]
        public async Task Actions_AfterLoad_ComeFromConfig()
        {
            await _vm.InitializeAsync();

            Assert.Equal(4, _vm.Actions.Count);
            Assert.Equal("ターミナルで開く", _vm.Actions[3].Name);
        }

        [Fact]
        public void OpenActionMenu_WithoutSelection_ReturnsFalse()
        {
            Assert.False(_vm.OpenActionMenu());
            Assert.False(_vm.IsActionMenuOpen);
        }

        [Fact]
        public async Task OpenActionMenu_ListsAllActionsForSelectedItem()
        {
            await _vm.InitializeAsync();

            Assert.True(_vm.OpenActionMenu());

            Assert.True(_vm.IsActionMenuOpen);
            Assert.Same(_vm.SelectedItem, _vm.ActionMenuTarget);
            Assert.Equal(4, _vm.ActionMenuItems.Count);
            Assert.Same(_vm.ActionMenuItems[0], _vm.SelectedActionMenuItem);
            Assert.Equal("Enter", _vm.ActionMenuItems[0].KeyText);
            Assert.Equal(string.Empty, _vm.ActionPattern);
        }

        [Fact]
        public async Task ActionPattern_FiltersMenuByName()
        {
            await _vm.InitializeAsync();
            _vm.OpenActionMenu();

            _vm.ActionPattern = "ターミナル";

            var only = Assert.Single(_vm.ActionMenuItems);
            Assert.Equal("ターミナルで開く", only.Action.Name);
            Assert.Equal(Enumerable.Range(0, 5), only.Match.Pos);
            Assert.Same(only, _vm.SelectedActionMenuItem);
        }

        [Fact]
        public async Task MoveSelection_WhileMenuOpen_MovesMenuSelectionOnly()
        {
            await _vm.InitializeAsync();
            var listSelection = _vm.SelectedItem;
            _vm.OpenActionMenu();

            _vm.MoveSelection(+2);
            Assert.Same(_vm.ActionMenuItems[2], _vm.SelectedActionMenuItem);

            _vm.MoveSelection(+10);
            Assert.Same(_vm.ActionMenuItems[3], _vm.SelectedActionMenuItem);

            _vm.MoveSelection(-10);
            Assert.Same(_vm.ActionMenuItems[0], _vm.SelectedActionMenuItem);

            Assert.Same(listSelection, _vm.SelectedItem);
        }

        [Fact]
        public async Task RunSelectedAction_RunsOnTargetAndClosesMenu()
        {
            await _vm.InitializeAsync();
            _vm.SelectedItem = _vm.FilteredResult.Single(r => r.Text == "Editor");
            _vm.OpenActionMenu();
            _vm.ActionPattern = "ターミナル";

            var outcome = _vm.RunSelectedAction();

            Assert.Equal(LaunchOutcome.Launched, outcome);
            Assert.False(_vm.IsActionMenuOpen);
            Assert.Null(_vm.ActionMenuTarget);
            var psi = Assert.Single(_processes.Started);
            Assert.Equal(@"C:\wt.exe", psi.FileName);
            Assert.Equal("-d \"C:\\Editor\"", psi.Arguments);
        }

        [Fact]
        public async Task RunSelectedAction_NoMatchingAction_ReturnsNothingSelected()
        {
            await _vm.InitializeAsync();
            _vm.OpenActionMenu();
            _vm.ActionPattern = "zzzz";

            Assert.Null(_vm.SelectedActionMenuItem);
            Assert.Equal(LaunchOutcome.NothingSelected, _vm.RunSelectedAction());
            Assert.False(_vm.IsActionMenuOpen);
        }

        [Fact]
        public async Task CloseActionMenu_ClearsTarget()
        {
            await _vm.InitializeAsync();
            _vm.OpenActionMenu();

            _vm.CloseActionMenu();

            Assert.False(_vm.IsActionMenuOpen);
            Assert.Null(_vm.ActionMenuTarget);
        }

        [Fact]
        public async Task RunAction_WhenPlannerFails_ReportsError()
        {
            await _vm.InitializeAsync();
            var broken = new LauncherAction("x", null, "nope", "{path}", "open");
            _vm.SelectedItem = new FilterResult { Type = ItemKind.Command, Text = "missing" };

            var outcome = _vm.RunAction(broken);

            Assert.Equal(LaunchOutcome.Failed, outcome);
            Assert.Single(_errors.Errors);
            Assert.Empty(_processes.Started);
        }
    }
}
