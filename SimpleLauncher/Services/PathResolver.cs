using System.IO;

namespace SimpleLauncher.Services
{
    public static class PathResolver
    {
        private static readonly string[] FallbackExtensions = { ".exe", ".bat", ".cmd" };

        /// <summary>
        /// 実行ファイル名を PATH 環境変数から探し、見つかれば絶対パスを返す。
        /// 絶対パスで渡された場合はそのファイルが存在すればそれを返す。
        /// </summary>
        /// <returns>絶対パス。見つからなければ null。</returns>
        public static string? FindExecutableInPath(string exeName) =>
            FindExecutableInPath(
                exeName,
                Environment.GetEnvironmentVariable("PATH"),
                GetPathExtensions(Environment.GetEnvironmentVariable("PATHEXT")));

        public static string? FindExecutableInPath(string exeName, string? pathEnv, IReadOnlyList<string> extensions)
        {
            if (string.IsNullOrWhiteSpace(exeName))
            {
                return null;
            }

            if (Path.IsPathRooted(exeName))
            {
                return File.Exists(exeName) ? Path.GetFullPath(exeName) : null;
            }

            if (string.IsNullOrEmpty(pathEnv))
            {
                return null;
            }

            var candidates = GetCandidateFileNames(exeName, extensions);

            foreach (var dir in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                foreach (var candidate in candidates)
                {
                    var fullPath = Path.Combine(dir, candidate);
                    if (File.Exists(fullPath))
                    {
                        return Path.GetFullPath(fullPath);
                    }
                }
            }

            return null;
        }

        /// <summary>PATHEXT の値 (";" 区切り) を拡張子の一覧に変換する。</summary>
        public static IReadOnlyList<string> GetPathExtensions(string? pathExt)
        {
            if (string.IsNullOrWhiteSpace(pathExt))
            {
                return FallbackExtensions;
            }

            return pathExt
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();
        }

        private static IReadOnlyList<string> GetCandidateFileNames(string exeName, IReadOnlyList<string> extensions)
        {
            var hasKnownExtension = extensions.Any(ext =>
                ext.Length > 0 && exeName.EndsWith(ext, StringComparison.OrdinalIgnoreCase));

            if (hasKnownExtension)
            {
                return new[] { exeName };
            }

            return extensions.Select(ext => exeName + ext).ToList();
        }
    }
}
