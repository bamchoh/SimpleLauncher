using SimpleLauncher.Config;

namespace SimpleLauncher.Tests
{
    public class AppSettingsTests
    {
        [Fact]
        public void Load_ReadsHotKeyFromJson()
        {
            using var temp = new TempDirectory();
            temp.WriteFile(AppSettings.FileName, "{ \"HotKey\": \"Ctrl+Shift+Space\" }");

            var settings = AppSettings.Load(temp.Path);

            Assert.Equal("Ctrl+Shift+Space", settings.HotKey);
        }

        [Fact]
        public void Load_MissingFile_UsesDefaultHotKey()
        {
            using var temp = new TempDirectory();

            var settings = AppSettings.Load(temp.Path);

            Assert.Equal(AppSettings.DefaultHotKey, settings.HotKey);
        }

        [Fact]
        public void Load_BlankHotKey_UsesDefaultHotKey()
        {
            using var temp = new TempDirectory();
            temp.WriteFile(AppSettings.FileName, "{ \"HotKey\": \"  \" }");

            var settings = AppSettings.Load(temp.Path);

            Assert.Equal(AppSettings.DefaultHotKey, settings.HotKey);
        }
    }
}
