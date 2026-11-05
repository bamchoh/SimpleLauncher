using System.Diagnostics;
using SimpleLauncher.Config;
using SimpleLauncher.Models;
using SimpleLauncher.Services;

namespace SimpleLauncher.Tests
{
    internal sealed class FakeShortcutResolver : IShortcutResolver
    {
        public Dictionary<string, string?> Targets { get; } = new(StringComparer.OrdinalIgnoreCase);

        public string? GetTargetPath(string shortcutPath) =>
            Targets.TryGetValue(shortcutPath, out var target) ? target : null;
    }

    internal sealed class FakeFilterService : IFilterService
    {
        public List<string> Items { get; private set; } = new();

        public List<string> Commands { get; private set; } = new();

        public List<string> Patterns { get; } = new();

        public Func<string, Task<IReadOnlyList<FilterResult>>> OnFilter { get; set; } =
            _ => Task.FromResult<IReadOnlyList<FilterResult>>(Array.Empty<FilterResult>());

        public Task SetListAsync(IReadOnlyList<string> items)
        {
            Items = items.ToList();
            return Task.CompletedTask;
        }

        public Task SetCommandListAsync(IReadOnlyList<string> items)
        {
            Commands = items.ToList();
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<FilterResult>> FilterAsync(string pattern)
        {
            Patterns.Add(pattern);
            return OnFilter(pattern);
        }
    }

    internal sealed class FakeInstalledAppProvider : IInstalledAppProvider
    {
        public Dictionary<string, LaunchableItem> Apps { get; } = new();

        public Exception? Failure { get; set; }

        public IReadOnlyDictionary<string, LaunchableItem> GetInstalledApps()
        {
            if (Failure != null)
            {
                throw Failure;
            }

            return new Dictionary<string, LaunchableItem>(Apps);
        }
    }

    internal sealed class FakeRecentFilesProvider : IRecentFilesProvider
    {
        public List<string> Files { get; } = new();

        public IReadOnlyList<string> GetRecentFiles() => Files.ToList();
    }

    internal sealed class FakeConfigProvider : ILauncherConfigProvider
    {
        public LauncherConfig Config { get; set; } = new();

        public LauncherConfig Load() => Config;
    }

    internal sealed class FakeProcessLauncher : IProcessLauncher
    {
        public List<ProcessStartInfo> Started { get; } = new();

        public Exception? Failure { get; set; }

        public void Start(ProcessStartInfo startInfo)
        {
            if (Failure != null)
            {
                throw Failure;
            }

            Started.Add(startInfo);
        }
    }

    internal sealed class FakeErrorReporter : IErrorReporter
    {
        public List<string> Errors { get; } = new();

        public void ShowError(string message) => Errors.Add(message);
    }

    /// <summary>テスト中だけ存在する一時ディレクトリ。</summary>
    internal sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "SimpleLauncher.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public string Combine(params string[] parts) =>
            System.IO.Path.Combine(new[] { Path }.Concat(parts).ToArray());

        public string WriteFile(string relativePath, string content = "")
        {
            var full = Combine(relativePath);
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(full)!);
            File.WriteAllText(full, content);
            return full;
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, recursive: true);
            }
            catch (IOException)
            {
                // 後始末に失敗してもテスト結果には影響しない
            }
        }
    }

    internal static class Items
    {
        public static FilterResult App(string text, int score = 100) =>
            new() { Type = ItemKind.App, Text = text, Score = score, Pos = new List<int> { -1 } };

        public static FilterResult Command(string text, int score = 100) =>
            new() { Type = ItemKind.Command, Text = text, Score = score, Pos = new List<int> { -1 } };

        public static FilterResult File(string text, int score = 100) =>
            new() { Type = ItemKind.File, Text = text, Score = score, Pos = new List<int> { -1 } };
    }
}
