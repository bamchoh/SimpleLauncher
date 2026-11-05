using SimpleLauncher.Services;

namespace SimpleLauncher.Tests
{
    public class PathResolverTests
    {
        private static readonly string[] Extensions = { ".exe", ".bat" };

        [Fact]
        public void FindExecutableInPath_AppendsExtensionAndSearchesEachDirectory()
        {
            using var temp = new TempDirectory();
            var expected = temp.WriteFile(Path.Combine("bin", "tool.exe"));
            var pathEnv = string.Join(Path.PathSeparator, temp.Combine("empty"), temp.Combine("bin"));

            var found = PathResolver.FindExecutableInPath("tool", pathEnv, Extensions);

            Assert.Equal(expected, found);
        }

        [Fact]
        public void FindExecutableInPath_KeepsExistingKnownExtension()
        {
            using var temp = new TempDirectory();
            var expected = temp.WriteFile("script.bat");

            var found = PathResolver.FindExecutableInPath("SCRIPT.BAT", temp.Path, Extensions);

            Assert.Equal(expected, found, ignoreCase: true);
        }

        [Fact]
        public void FindExecutableInPath_TriesExtensionsInOrder()
        {
            using var temp = new TempDirectory();
            temp.WriteFile("tool.bat");
            var expected = temp.WriteFile("tool.exe");

            var found = PathResolver.FindExecutableInPath("tool", temp.Path, Extensions);

            Assert.Equal(expected, found);
        }

        [Fact]
        public void FindExecutableInPath_ReturnsNullWhenMissing()
        {
            using var temp = new TempDirectory();

            Assert.Null(PathResolver.FindExecutableInPath("missing", temp.Path, Extensions));
            Assert.Null(PathResolver.FindExecutableInPath("missing", null, Extensions));
            Assert.Null(PathResolver.FindExecutableInPath("", temp.Path, Extensions));
        }

        [Fact]
        public void FindExecutableInPath_RootedPath_ReturnsItWhenItExists()
        {
            using var temp = new TempDirectory();
            var existing = temp.WriteFile("app.exe");

            Assert.Equal(existing, PathResolver.FindExecutableInPath(existing, "ignored", Extensions));
            Assert.Null(PathResolver.FindExecutableInPath(temp.Combine("nope.exe"), "ignored", Extensions));
        }

        [Fact]
        public void GetPathExtensions_ParsesSemicolonSeparatedList()
        {
            var extensions = PathResolver.GetPathExtensions(".COM;.EXE; .BAT;");

            Assert.Equal(new[] { ".COM", ".EXE", ".BAT" }, extensions);
        }

        [Fact]
        public void GetPathExtensions_FallsBackWhenUnset()
        {
            var extensions = PathResolver.GetPathExtensions(null);

            Assert.Contains(".exe", extensions);
        }
    }
}
