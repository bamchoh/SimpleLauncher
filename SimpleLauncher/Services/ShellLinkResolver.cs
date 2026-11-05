using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;

namespace SimpleLauncher.Services
{
    /// <summary>
    /// IShellLinkW を直接呼び出して .lnk のリンク先を取得する。
    /// tlbimp を必要としないので dotnet CLI だけでビルドできる。
    /// </summary>
    public sealed class ShellLinkResolver : IShortcutResolver
    {
        private const int MaxPath = 260;

        public string? GetTargetPath(string shortcutPath)
        {
            try
            {
                var link = (IShellLinkW)new ShellLink();
                ((IPersistFile)link).Load(shortcutPath, 0);

                var buffer = new StringBuilder(MaxPath);
                link.GetPath(buffer, buffer.Capacity, IntPtr.Zero, 0);

                var target = buffer.ToString();
                return string.IsNullOrEmpty(target) ? null : target;
            }
            catch (Exception ex) when (ex is COMException or ArgumentException or UnauthorizedAccessException or IOException)
            {
                // 壊れたショートカット等はスキップ
                Debug.WriteLine($"Failed to resolve shortcut: {shortcutPath} ({ex.Message})");
                return null;
            }
        }

        [ComImport]
        [Guid("00021401-0000-0000-C000-000000000046")]
        private class ShellLink
        {
        }

        [ComImport]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [Guid("000214F9-0000-0000-C000-000000000046")]
        private interface IShellLinkW
        {
            void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cch, IntPtr pfd, uint fFlags);
            void GetIDList(out IntPtr ppidl);
            void SetIDList(IntPtr pidl);
            void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cch);
            void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
            void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cch);
            void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
            void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cch);
            void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
            void GetHotkey(out short pwHotkey);
            void SetHotkey(short wHotkey);
            void GetShowCmd(out int piShowCmd);
            void SetShowCmd(int iShowCmd);
            void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cch, out int piIcon);
            void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
            void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, uint dwReserved);
            void Resolve(IntPtr hwnd, uint fFlags);
            void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
        }
    }
}
