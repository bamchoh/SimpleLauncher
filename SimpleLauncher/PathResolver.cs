using System;
using System.IO;

namespace SimpleLauncher
{
    public class PathResolver
    {
        /// <summary>
        /// 指定された実行ファイル名が PATH 環境変数上に存在するかをチェックし、
        /// 存在すれば絶対パスを返す。存在しなければ null を返す。
        /// </summary>
        /// <param name="exeName">実行ファイル名（拡張子なしでもOK）</param>
        /// <returns>絶対パス（見つかった場合）、または null</returns>
        public static string? FindExecutableInPath(string exeName)
        {
            // すでに絶対パス（フルパス）で渡された場合は、そのファイルが存在すればそれを返す
            if (Path.IsPathRooted(exeName))
            {
                if (File.Exists(exeName) && IsExecutable(exeName))
                {
                    return Path.GetFullPath(exeName);
                }
                return null;
            }

            // Windowsの場合、拡張子がない場合は .exe を補う
            var extensions = Environment.OSVersion.Platform == PlatformID.Win32NT
                ? (Environment.GetEnvironmentVariable("PATHEXT")?.Split(';').ToList() ?? new List<string>() { ".exe", ".bat", ".cmd" })
                : new List<string>() { "" }; // UNIX系では拡張子を使わないことが多い

            // PATH 環境変数の取得
            string? pathEnv = Environment.GetEnvironmentVariable("PATH");
            if (pathEnv == null) return null;

            string[] paths = pathEnv.Split(Path.PathSeparator);

            foreach (var dir in paths)
            {
                foreach (var ext in extensions)
                {
                    var filename = exeName.ToLower().EndsWith(ext.ToLower()) ? exeName : exeName + ext;
                    string fullPath = Path.Combine(dir, filename);
                    if (File.Exists(fullPath) && IsExecutable(fullPath))
                    {
                        return Path.GetFullPath(fullPath);
                    }
                }
            }

            return null;
        }

        private static bool IsExecutable(string path)
        {
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                return true; // Windowsでは存在すれば実行可能とみなす
            }
            else
            {
                // UNIX系では実行属性をチェック
                return (new FileInfo(path).Exists &&
                        (new FileInfo(path).Attributes & FileAttributes.Directory) == 0 &&
                        (new FileInfo(path).Attributes & FileAttributes.Normal) == FileAttributes.Normal);
            }
        }
    }
}
