using SimpleLauncher.Services;

namespace SimpleLauncher.Tests
{
    public class RecentFilesScannerTests
    {
        [Fact]
        public void GetRecentFiles_ReturnsTargetsNewestFirstWithoutDuplicatesOrBrokenLinks()
        {
            using var temp = new TempDirectory();
            var resolver = new FakeShortcutResolver();
            var now = DateTime.UtcNow;

            var oldest = temp.WriteFile("c.lnk");
            var middle = temp.WriteFile("a.lnk");
            var newest = temp.WriteFile("b.lnk");
            var broken = temp.WriteFile("d.lnk");
            temp.WriteFile("not-a-shortcut.txt");

            File.SetLastWriteTimeUtc(oldest, now.AddMinutes(-3));
            File.SetLastWriteTimeUtc(middle, now.AddMinutes(-2));
            File.SetLastWriteTimeUtc(newest, now.AddMinutes(-1));
            File.SetLastWriteTimeUtc(broken, now);

            resolver.Targets[oldest] = @"C:\docs\x.txt";
            resolver.Targets[middle] = @"c:\DOCS\X.TXT"; // 同じファイルへの重複
            resolver.Targets[newest] = @"C:\docs\y.txt";
            resolver.Targets[broken] = null;

            var files = new RecentFilesScanner(resolver, temp.Path).GetRecentFiles();

            Assert.Equal(new[] { @"C:\docs\y.txt", @"c:\DOCS\X.TXT" }, files);
        }

        [Fact]
        public void GetRecentFiles_MissingDirectory_ReturnsEmpty()
        {
            using var temp = new TempDirectory();

            var files = new RecentFilesScanner(new FakeShortcutResolver(), temp.Combine("missing")).GetRecentFiles();

            Assert.Empty(files);
        }
    }

    public class InstalledAppScannerTests
    {
        [Theory]
        [InlineData(@"C:\Program Files\App\app.exe", true)]
        [InlineData(@"C:\Program Files\App\APP.EXE", true)]
        [InlineData("shell:AppsFolder\\Microsoft.WindowsCalculator_8wekyb3d8bbwe!App", true)]
        [InlineData(@"C:\docs\readme.txt", false)]
        [InlineData("", false)]
        [InlineData(null, false)]
        public void IsLaunchableTarget_AcceptsExeAndUwpOnly(string? target, bool expected)
        {
            Assert.Equal(expected, InstalledAppScanner.IsLaunchableTarget(target));
        }

        [Fact]
        public void IsUninstaller_MatchesCaseInsensitively()
        {
            Assert.True(InstalledAppScanner.IsUninstaller("Uninstall Foo"));
            Assert.True(InstalledAppScanner.IsUninstaller("foo UNINSTALLER"));
            Assert.False(InstalledAppScanner.IsUninstaller("Foo"));
        }

        [Fact]
        public void EnumerateShortcuts_RecursesAndIgnoresOtherFiles()
        {
            using var temp = new TempDirectory();
            var top = temp.WriteFile(Path.Combine("menu", "Top.lnk"));
            var nested = temp.WriteFile(Path.Combine("menu", "Sub", "Deep", "Nested.lnk"));
            temp.WriteFile(Path.Combine("menu", "Sub", "readme.txt"));

            var found = InstalledAppScanner.EnumerateShortcuts(temp.Combine("menu")).ToList();

            Assert.Equal(2, found.Count);
            Assert.Contains(top, found);
            Assert.Contains(nested, found);
        }

        [Fact]
        public void EnumerateShortcuts_MissingRoot_ReturnsEmpty()
        {
            Assert.Empty(InstalledAppScanner.EnumerateShortcuts(@"C:\definitely\not\here"));
        }

        [Fact]
        public void GetInstalledApps_CollectsStartMenuAppsAndDesktopFiles()
        {
            using var temp = new TempDirectory();
            var resolver = new FakeShortcutResolver();

            var app = temp.WriteFile(Path.Combine("menu", "Programs", "Editor.lnk"));
            var uwp = temp.WriteFile(Path.Combine("menu", "Calc.lnk"));
            var uninstaller = temp.WriteFile(Path.Combine("menu", "Uninstall Editor.lnk"));
            var document = temp.WriteFile(Path.Combine("menu", "Manual.lnk"));
            var broken = temp.WriteFile(Path.Combine("menu", "Broken.lnk"));
            var desktopFile = temp.WriteFile(Path.Combine("desktop", "todo.txt"));
            temp.WriteFile(Path.Combine("desktop", "folder", "inside.txt"));

            resolver.Targets[app] = @"C:\Editor\editor.exe";
            resolver.Targets[uwp] = "shell:AppsFolder\\Calc!App";
            resolver.Targets[uninstaller] = @"C:\Editor\uninstall.exe";
            resolver.Targets[document] = @"C:\Editor\manual.pdf";

            var scanner = new InstalledAppScanner(
                resolver,
                new[] { temp.Combine("menu"), temp.Combine("missing-menu") },
                new[] { temp.Combine("desktop"), temp.Combine("missing-desktop") });

            var apps = scanner.GetInstalledApps();

            Assert.Equal(3, apps.Count);
            Assert.Equal(app, apps["Editor"].Path);
            Assert.Equal(uwp, apps["Calc"].Path);
            Assert.Equal(desktopFile, apps[desktopFile].Path);
        }
    }
}
