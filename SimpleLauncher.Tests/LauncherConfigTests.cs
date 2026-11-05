using SimpleLauncher.Config;

namespace SimpleLauncher.Tests
{
    public class LauncherConfigTests
    {
        [Fact]
        public void CommandInfoParse_SplitsNameExecAndArgs()
        {
            var command = CommandInfo.Parse("google\nchrome\nhttps://www.google.com");

            Assert.NotNull(command);
            Assert.Equal("google", command!.Name);
            Assert.Equal("chrome", command.Exec);
            Assert.Equal("https://www.google.com", command.Args);
        }

        [Fact]
        public void CommandInfoParse_WithoutArgs_UsesEmptyArgs()
        {
            var command = CommandInfo.Parse("power shell\npowershell");

            Assert.Equal(string.Empty, command!.Args);
        }

        [Fact]
        public void CommandInfoParse_MultiLineArgs_ArePreserved()
        {
            var command = CommandInfo.Parse("x\nexec\narg1\narg2");

            Assert.Equal("arg1\narg2", command!.Args);
        }

        [Fact]
        public void CommandInfoParse_SingleLine_ReturnsNull()
        {
            Assert.Null(CommandInfo.Parse("only-a-name"));
        }

        [Fact]
        public void BuildCommandList_AddsEditCommandPointingAtConfigFile()
        {
            var config = new LauncherConfig();

            config.BuildCommandList(@"C:\conf\simple-launcher.yaml");

            var edit = config.CommandList[LauncherConfig.EditCommandName];
            Assert.Equal("explorer", edit.Exec);
            Assert.Equal("\"C:\\conf\\simple-launcher.yaml\"", edit.Args);
        }

        [Fact]
        public void BuildCommandList_SkipsMalformedEntries()
        {
            var config = new LauncherConfig { List = { "bad", "good\nexec" } };

            config.BuildCommandList("conf.yaml");

            Assert.Equal(2, config.CommandList.Count);
            Assert.Contains("good", config.CommandList.Keys);
            Assert.DoesNotContain("bad", config.CommandList.Keys);
        }

        [Fact]
        public void GetExecFromAlias_ResolvesAliasOrReturnsInput()
        {
            var config = new LauncherConfig { Alias = { ["chrome"] = @"C:\chrome.exe" } };

            Assert.Equal(@"C:\chrome.exe", config.GetExecFromAlias("chrome"));
            Assert.Equal("notepad", config.GetExecFromAlias("notepad"));
        }

        [Fact]
        public void LoaderParse_ReadsYamlAndBuildsCommands()
        {
            const string yaml = """
                version: 1
                alias:
                  chrome: C:\Program Files\Google\Chrome\Application\chrome.exe
                bindings:
                  - ignored-legacy-key
                list:
                  - |-
                    power shell
                    powershell
                  - |-
                    google.com
                    chrome
                    https://www.google.com
                """;

            var config = LauncherConfigLoader.Parse(yaml, "conf.yaml");

            Assert.Equal(1, config.Version);
            Assert.Equal(@"C:\Program Files\Google\Chrome\Application\chrome.exe", config.Alias["chrome"]);
            Assert.Equal(3, config.CommandList.Count);
            Assert.Equal("https://www.google.com", config.CommandList["google.com"].Args);
            Assert.Equal("powershell", config.CommandList["power shell"].Exec);
        }

        [Fact]
        public void LoaderParse_EmptyYaml_ReturnsDefaultsWithEditCommand()
        {
            var config = LauncherConfigLoader.Parse("", "conf.yaml");

            Assert.Empty(config.Alias);
            var only = Assert.Single(config.CommandList);
            Assert.Equal(LauncherConfig.EditCommandName, only.Key);
        }

        [Fact]
        public void AppDataConfigStore_CopiesDefaultWhenConfigMissing()
        {
            using var temp = new TempDirectory();
            var defaultPath = temp.WriteFile("launcher.yaml", "version: 7\nlist:\n  - |-\n    a\n    b\n");
            var store = new AppDataConfigStore(temp.Combine("appdata"), defaultPath);

            var config = store.Load();

            Assert.Equal(7, config.Version);
            Assert.True(File.Exists(store.ConfigPath));
            Assert.Contains("a", config.CommandList.Keys);
        }

        [Fact]
        public void AppDataConfigStore_LoadsExistingConfigInsteadOfDefault()
        {
            using var temp = new TempDirectory();
            var defaultPath = temp.WriteFile("launcher.yaml", "version: 1\n");
            var store = new AppDataConfigStore(temp.Combine("appdata"), defaultPath);
            temp.WriteFile(Path.Combine("appdata", AppDataConfigStore.ConfigFileName), "version: 2\n");

            var config = store.Load();

            Assert.Equal(2, config.Version);
        }

        [Fact]
        public void AppDataConfigStore_WritesMinimalConfigWhenDefaultMissing()
        {
            using var temp = new TempDirectory();
            var store = new AppDataConfigStore(temp.Combine("appdata"), temp.Combine("no-such-default.yaml"));

            var config = store.Load();

            Assert.True(File.Exists(store.ConfigPath));
            Assert.Single(config.CommandList);
        }
    }
}
