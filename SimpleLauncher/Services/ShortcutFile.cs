using System.IO;

namespace SimpleLauncher.Services
{
    public static class ShortcutFile
    {
        public const string Extension = ".lnk";

        public static bool IsShortcut(string path) =>
            string.Equals(Path.GetExtension(path), Extension, StringComparison.OrdinalIgnoreCase);
    }
}
