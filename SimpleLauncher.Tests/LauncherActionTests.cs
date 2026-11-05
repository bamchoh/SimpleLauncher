using SimpleLauncher.Config;
using SimpleLauncher.Interop;
using SimpleLauncher.Models;
using SimpleLauncher.Services;

namespace SimpleLauncher.Tests
{
    public class LauncherActionTests
    {
        [Fact]
        public void Merge_WithoutCustomActions_KeepsDefaults()
        {
            var merged = LauncherAction.Merge(LauncherAction.Defaults, Array.Empty<LauncherAction>());

            Assert.Equal(new[] { "開く", "管理者として実行", "場所を開く" }, merged.Select(a => a.Name));
            Assert.Equal(new[] { "Enter", "Ctrl+Enter", "Ctrl+I" }, merged.Select(a => a.Key));
        }

        [Fact]
        public void Merge_SameName_ReplacesInPlace()
        {
            var custom = new LauncherAction("管理者として実行", "Shift+Enter", null, "", "runas");

            var merged = LauncherAction.Merge(LauncherAction.Defaults, new[] { custom });

            Assert.Equal(3, merged.Count);
            Assert.Same(custom, merged[1]);
        }

        [Fact]
        public void Merge_NewName_IsAppended()
        {
            var custom = new LauncherAction("ターミナルで開く", "Ctrl+T", "wt", "-d \"{dir}\"", "open");

            var merged = LauncherAction.Merge(LauncherAction.Defaults, new[] { custom });

            Assert.Equal(4, merged.Count);
            Assert.Same(custom, merged[3]);
        }

        [Fact]
        public void Merge_DuplicateKey_LaterActionWinsAndEarlierLosesKey()
        {
            var custom = new LauncherAction("別の操作", "ctrl+i", "foo", "", "open");

            var merged = LauncherAction.Merge(LauncherAction.Defaults, new[] { custom });

            Assert.Null(merged.Single(a => a.Name == "場所を開く").Key);
            Assert.Equal("ctrl+i", merged.Single(a => a.Name == "別の操作").Key);
        }

        [Fact]
        public void Merge_BlankKey_BecomesNull()
        {
            var custom = new LauncherAction("x", "  ", "foo", "", "open");

            var merged = LauncherAction.Merge(Array.Empty<LauncherAction>(), new[] { custom });

            Assert.Null(merged.Single().Key);
        }

        [Fact]
        public void DefaultKeys_AreParseable()
        {
            foreach (var action in LauncherAction.Defaults)
            {
                Assert.True(HotKeyData.TryParse(action.Key, out _), action.Name);
            }
        }

        [Fact]
        public void Config_ParsesActionsAndMergesWithDefaults()
        {
            const string yaml = """
                actions:
                  - name: ターミナルで開く
                    key: Ctrl+T
                    exec: wt
                    args: -d "{dir}"
                  - name: 場所を開く
                    key: Ctrl+L
                    exec: explorer
                    args: /select,"{path}"
                  - name: 既定の動詞
                    exec: foo
                  - key: Ctrl+X
                    exec: nameless-is-skipped
                """;

            var config = LauncherConfigLoader.Parse(yaml, "conf.yaml");

            Assert.Equal(new[] { "開く", "管理者として実行", "場所を開く", "ターミナルで開く", "既定の動詞" },
                config.ActionList.Select(a => a.Name));
            Assert.Equal("Ctrl+L", config.ActionList[2].Key);
            Assert.Equal("wt", config.ActionList[3].Exec);
            Assert.Equal("-d \"{dir}\"", config.ActionList[3].Args);
            Assert.Equal(LauncherAction.DefaultVerb, config.ActionList[4].Verb);
            Assert.Null(config.ActionList[4].Key);
        }

        [Fact]
        public void Config_WithoutActions_HasDefaults()
        {
            var config = LauncherConfigLoader.Parse("version: 1\n", "conf.yaml");

            Assert.Equal(LauncherAction.Defaults, config.ActionList);
        }
    }

    public class LaunchPlannerActionTests
    {
        private readonly FakeShortcutResolver _resolver = new();
        private readonly LaunchPlanner _planner;
        private readonly LaunchCatalog _catalog;

        public LaunchPlannerActionTests()
        {
            _planner = new LaunchPlanner(
                _resolver,
                name => name switch
                {
                    "explorer" => @"C:\Windows\explorer.exe",
                    "wt" => @"C:\Tools\wt.exe",
                    _ => null,
                },
                path => path == @"C:\Users\me\Downloads");

            var config = LauncherConfigLoader.Parse("""
                alias:
                  code: C:\VSCode\Code.exe
                list:
                  - |-
                    downloads
                    explorer
                    "C:\Users\me\Downloads"
                  - |-
                    shell
                    powershell
                """, @"C:\conf\simple-launcher.yaml");

            var apps = new Dictionary<string, LaunchableItem>
            {
                ["Editor"] = new("Editor", @"C:\menu\Editor.lnk"),
            };
            _resolver.Targets[@"C:\menu\Editor.lnk"] = @"C:\Editor\bin\editor.exe";

            _catalog = new LaunchCatalog(apps, config);
        }

        private static LauncherAction Terminal => new("ターミナルで開く", "Ctrl+T", "wt", "-d \"{dir}\"", "open");

        private static LauncherAction Code => new("VS Code で開く", null, "code", "\"{dir}\"", "open");

        [Fact]
        public void CreateActionInfo_SubstitutesDirForApp()
        {
            var psi = _planner.CreateActionInfo(Terminal, Items.App("Editor"), _catalog);

            Assert.Equal(@"C:\Tools\wt.exe", psi.FileName);
            Assert.Equal("-d \"C:\\Editor\\bin\"", psi.Arguments);
            Assert.Equal("open", psi.Verb);
            Assert.True(psi.UseShellExecute);
        }

        [Fact]
        public void CreateActionInfo_SubstitutesPathForFile()
        {
            var action = new LauncherAction("x", null, "explorer", "/select,\"{path}\"", "open");

            var psi = _planner.CreateActionInfo(action, Items.File(@"C:\docs\notes.txt"), _catalog);

            Assert.Equal("/select,\"C:\\docs\\notes.txt\"", psi.Arguments);
        }

        [Fact]
        public void CreateActionInfo_ResolvesExecThroughAlias()
        {
            var psi = _planner.CreateActionInfo(Code, Items.File(@"C:\docs\notes.txt"), _catalog);

            Assert.Equal(@"C:\VSCode\Code.exe", psi.FileName);
            Assert.Equal("\"C:\\docs\"", psi.Arguments);
        }

        [Fact]
        public void CreateActionInfo_DirOfDirectoryIsItself()
        {
            var psi = _planner.CreateActionInfo(Terminal, Items.Command("downloads"), _catalog);

            // コマンドの引数の引用符は外され、フォルダ自身が {dir} になる
            Assert.Equal("-d \"C:\\Users\\me\\Downloads\"", psi.Arguments);
        }

        [Fact]
        public void CreateActionInfo_CommandWithoutPath_Throws()
        {
            Assert.Throws<LaunchException>(() => _planner.CreateActionInfo(Terminal, Items.Command("shell"), _catalog));
        }

        [Fact]
        public void CreateActionInfo_LaunchActions_UseItemAndVerb()
        {
            var open = _planner.CreateActionInfo(LauncherAction.Open, Items.App("Editor"), _catalog);
            var admin = _planner.CreateActionInfo(LauncherAction.RunAsAdmin, Items.Command("shell"), _catalog);

            Assert.Equal(@"C:\menu\Editor.lnk", open.FileName);
            Assert.Equal("open", open.Verb);
            Assert.Equal("powershell", admin.FileName);
            Assert.Equal("runas", admin.Verb);
        }

        [Fact]
        public void CreateActionInfo_PlaceholdersAreCaseInsensitive()
        {
            var action = new LauncherAction("x", null, "wt", "{PATH} {Dir}", "open");

            var psi = _planner.CreateActionInfo(action, Items.File(@"C:\docs\notes.txt"), _catalog);

            Assert.Equal(@"C:\docs\notes.txt C:\docs", psi.Arguments);
        }
    }
}
