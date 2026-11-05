using SimpleLauncher.Config;
using SimpleLauncher.Models;
using SimpleLauncher.Services;

namespace SimpleLauncher.Tests
{
    public class LaunchPlannerTests
    {
        private const string ExplorerPath = @"C:\Windows\explorer.exe";

        private readonly FakeShortcutResolver _resolver = new();
        private readonly LaunchPlanner _planner;
        private readonly LaunchCatalog _catalog;

        public LaunchPlannerTests()
        {
            _planner = new LaunchPlanner(_resolver, name => name == "explorer" ? ExplorerPath : null, path => path.EndsWith("Downloads"));

            var config = LauncherConfigLoader.Parse("""
                alias:
                  chrome: C:\chrome\chrome.exe
                list:
                  - |-
                    google
                    chrome
                    https://www.google.com
                  - |-
                    downloads
                    explorer
                    C:\Users\me\Downloads
                  - |-
                    shell
                    powershell
                """, @"C:\conf\simple-launcher.yaml");

            var apps = new Dictionary<string, LaunchableItem>
            {
                ["Editor"] = new("Editor", @"C:\menu\Editor.lnk"),
                [@"C:\Desktop\todo.txt"] = new(@"C:\Desktop\todo.txt", @"C:\Desktop\todo.txt"),
            };

            _catalog = new LaunchCatalog(apps, config);
        }

        [Fact]
        public void CreateLaunchInfo_App_OpensPathWithShellAndVerb()
        {
            var psi = _planner.CreateLaunchInfo(Items.App("Editor"), _catalog, "runas");

            Assert.Equal(@"C:\menu\Editor.lnk", psi.FileName);
            Assert.True(psi.UseShellExecute);
            Assert.Equal("runas", psi.Verb);
        }

        [Fact]
        public void CreateLaunchInfo_File_OpensThePathItself()
        {
            var psi = _planner.CreateLaunchInfo(Items.File(@"C:\docs\notes.txt"), _catalog);

            Assert.Equal(@"C:\docs\notes.txt", psi.FileName);
            Assert.Equal("open", psi.Verb);
        }

        [Fact]
        public void CreateLaunchInfo_Command_ResolvesAliasAndPassesArgs()
        {
            var psi = _planner.CreateLaunchInfo(Items.Command("google"), _catalog);

            // 別名は解決されるが PATH に無いので別名の値がそのまま使われる
            Assert.Equal(@"C:\chrome\chrome.exe", psi.FileName);
            Assert.Equal("https://www.google.com", psi.Arguments);
            Assert.True(psi.UseShellExecute);
        }

        [Fact]
        public void CreateLaunchInfo_Command_UsesResolvedExecutableFromPath()
        {
            var psi = _planner.CreateLaunchInfo(Items.Command("downloads"), _catalog);

            Assert.Equal(ExplorerPath, psi.FileName);
            Assert.Equal(@"C:\Users\me\Downloads", psi.Arguments);
        }

        [Fact]
        public void CreateLaunchInfo_UnknownApp_Throws()
        {
            Assert.Throws<LaunchException>(() => _planner.CreateLaunchInfo(Items.App("Missing"), _catalog));
            Assert.Throws<LaunchException>(() => _planner.CreateLaunchInfo(Items.Command("missing"), _catalog));
        }

        [Fact]
        public void CreateLaunchInfo_UnknownType_Throws()
        {
            var item = new FilterResult { Type = "mystery", Text = "x" };

            Assert.Throws<LaunchException>(() => _planner.CreateLaunchInfo(item, _catalog));
        }

        [Fact]
        public void OpenLocationAction_App_SelectsShortcutTargetInExplorer()
        {
            _resolver.Targets[@"C:\menu\Editor.lnk"] = @"C:\Editor\editor.exe";

            var psi = _planner.CreateActionInfo(LauncherAction.OpenLocation, Items.App("Editor"), _catalog);

            Assert.Equal(ExplorerPath, psi.FileName);
            Assert.Equal("/select,\"C:\\Editor\\editor.exe\"", psi.Arguments);
        }

        [Fact]
        public void OpenLocationAction_App_FallsBackToShortcutWhenUnresolvable()
        {
            var psi = _planner.CreateActionInfo(LauncherAction.OpenLocation, Items.App("Editor"), _catalog);

            Assert.Equal("/select,\"C:\\menu\\Editor.lnk\"", psi.Arguments);
        }

        [Fact]
        public void OpenLocationAction_DesktopFile_SelectsItDirectly()
        {
            var psi = _planner.CreateActionInfo(LauncherAction.OpenLocation, Items.App(@"C:\Desktop\todo.txt"), _catalog);

            Assert.Equal("/select,\"C:\\Desktop\\todo.txt\"", psi.Arguments);
        }

        [Fact]
        public void OpenLocationAction_RecentFile_SelectsIt()
        {
            var psi = _planner.CreateActionInfo(LauncherAction.OpenLocation, Items.File(@"C:\docs\notes.txt"), _catalog);

            Assert.Equal(ExplorerPath, psi.FileName);
            Assert.Equal("/select,\"C:\\docs\\notes.txt\"", psi.Arguments);
        }

        [Fact]
        public void OpenLocationAction_ExplorerCommand_SelectsItsArgument()
        {
            var psi = _planner.CreateActionInfo(LauncherAction.OpenLocation, Items.Command("downloads"), _catalog);

            Assert.Equal("/select,\"C:\\Users\\me\\Downloads\"", psi.Arguments);
        }

        [Fact]
        public void OpenLocationAction_EditCommand_DoesNotDoubleQuote()
        {
            var psi = _planner.CreateActionInfo(LauncherAction.OpenLocation, Items.Command(LauncherConfig.EditCommandName), _catalog);

            Assert.Equal("/select,\"C:\\conf\\simple-launcher.yaml\"", psi.Arguments);
        }

        [Fact]
        public void OpenLocationAction_NonExplorerCommand_Throws()
        {
            var ex = Assert.Throws<LaunchException>(() => _planner.CreateActionInfo(LauncherAction.OpenLocation, Items.Command("shell"), _catalog));

            Assert.Contains("powershell", ex.Message);
        }
    }
}
