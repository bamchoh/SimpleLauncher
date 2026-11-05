using SimpleLauncher.Services;

namespace SimpleLauncher.Tests
{
    /// <summary>実際の COM (IShellLinkW) を使う。Windows のスタートメニューにあるショートカットで確認する。</summary>
    public class ShellLinkResolverTests
    {
        [Fact]
        public void GetTargetPath_ResolvesARealStartMenuShortcut()
        {
            var roots = new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),
                Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
            };
            var shortcuts = roots.SelectMany(InstalledAppScanner.EnumerateShortcuts).Take(50).ToList();
            if (shortcuts.Count == 0)
            {
                return; // ショートカットが無い環境では検証できない
            }

            var resolver = new ShellLinkResolver();
            var targets = shortcuts.Select(resolver.GetTargetPath).Where(t => t != null).ToList();

            Assert.NotEmpty(targets);
            Assert.All(targets, t => Assert.False(string.IsNullOrWhiteSpace(t)));
        }

        [Fact]
        public void GetTargetPath_MissingFile_ReturnsNull()
        {
            using var temp = new TempDirectory();

            Assert.Null(new ShellLinkResolver().GetTargetPath(temp.Combine("missing.lnk")));
        }

        [Fact]
        public void GetTargetPath_NotAShortcut_ReturnsNull()
        {
            using var temp = new TempDirectory();
            var bogus = temp.WriteFile("bogus.lnk", "this is not a shell link");

            Assert.Null(new ShellLinkResolver().GetTargetPath(bogus));
        }
    }
}
