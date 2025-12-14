using Microsoft.Win32;
using StreamJsonRpc;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Prism.Mvvm;

namespace SimpleLauncher
{
    // 引数指定用のクラスを定義する
    public class SetListArgs
    {
        public List<string> List;
    }

    public class SetListReply
    {
        public int Result;
    }

    public class FilterArgs
    {
        public string Pattern;
    }

    public class FilterReply
    {
        public List<FilterResult> Results;
    }

    public class FilterResult
    {
        public string Type;
        public string Text;
        public int Score;
        public List<int> Pos;

        public override string ToString()
        {
            return $"{Text}";
        }
    }

    public class LaunchableItem
    {
        public string DisplayName { get; set; }
        public string Path { get; set; }
    }

    public class LaunchableItems : Dictionary<string, LaunchableItem>
    {
    }

    class MainWindowVM : BindableBase
    {
        private List<FilterResult> _filteredResult = new List<FilterResult>();
        public List<FilterResult> FilteredResult
        {
            get { return _filteredResult; }
            private set {
                SetProperty(ref _filteredResult, value);

                if (_filteredResult.Count > 0)
                {
                    SelectedItem = _filteredResult[0];
                }
            }
        }

        private FilterResult _selectedItem;
        public FilterResult SelectedItem
        {
            get { return _selectedItem; }
            set { SetProperty(ref _selectedItem, value); }
        }

        private string _pattern = string.Empty;
        public string Pattern
        {
            get { return _pattern; }
            set {
                SetProperty(ref _pattern, value);

                if(string.IsNullOrEmpty(_pattern))
                {
                    UpdateFilteredResult();

                    return;
                }

                ExecuteFilter();
            }
        }

        private LaunchableItems launchableItems;

        private YamlData yamlData;

        public void Execute(FilterResult filterResult, string verb = "open")
        {
            switch(filterResult.Type)
            {
                case "list":
                    var path = launchableItems[filterResult.Text].Path;

                    // ファイルを関連付けアプリで開く
                    var psiExplorer = new ProcessStartInfo()
                    {
                        FileName = path,
                        UseShellExecute = true,
                        Verb = verb,   // Explorer と同じ
                    };

                    Process.Start(psiExplorer);

                    break;
                case "command":
                    var cmd = yamlData.CommandList[filterResult.Text];

                    if (cmd.Exec == "(ff)")
                    {
                        // execFileFilter(cmd.Args);
                        return;
                    }

                    try
                    {
                        var newYamlData = yamlData.GetExecFromAlias(cmd.Exec);
                        var fileName = PathResolver.FindExecutableInPath(newYamlData);
                        var psi2 = new ProcessStartInfo()
                        {
                            FileName = fileName ?? cmd.Exec,
                            Arguments = cmd.Args,
                            UseShellExecute = true,
                            Verb = verb,   // Explorer と同じ
                        };

                        Process.Start(psi2);
                    }
                    catch (System.Exception ex)
                    {
                        System.Windows.MessageBox.Show(ex.Message, "実行エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    break;
                default:
                    break;
            }
        }

        private NamedPipeClientStream stream;

        private JsonRpc rpc;

        public MainWindowVM()
        {
            launchableItems = new LaunchableItems();

            Task.Run(() =>
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "tiny-fzf",
                    Arguments = "",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };

                Process.Start(psi).WaitForExit();
            });

            stream = new NamedPipeClientStream(".", "winiotestpipe", PipeDirection.InOut, PipeOptions.Asynchronous);

            stream.ConnectAsync().Wait(); // TODO: 非同期化する。パイプが無いと止まる。

            rpc = JsonRpc.Attach(stream);

            UpdateFilteredResult();
        }

        public void UpdateFilteredResult()
        {
            FilteredResult = CreateOriginalList();

            FilteredResult.AddRange(LoadConfigYaml());

            FilteredResult.Sort((a, b) =>
            {
                return string.Compare(a.Text, b.Text, StringComparison.OrdinalIgnoreCase);
            });

            SelectedItem = _filteredResult[0];
        }

        private List<FilterResult> CreateOriginalList()
        {
            launchableItems = GetInstalledApps();

            // ユーザー自身のデスクトップ
            string userDesktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);

            // 全ユーザー共通（パブリック）のデスクトップ
            string publicDesktop = Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);

            // デスクトップ直下のファイル一覧（フォルダは含まない）
            var files = Directory.GetFiles(userDesktop).ToList();

            files.AddRange(Directory.GetFiles(publicDesktop).ToList());

            for (int i = 0; i < files.Count; i++)
            {
                var launchableItem = new LaunchableItem
                {
                    DisplayName = files[i],
                    Path = files[i]
                };

                launchableItems[launchableItem.DisplayName] = launchableItem;
            }

            rpc.InvokeAsync<SetListReply>("TestRPC.SetList", new SetListArgs { List = launchableItems.Keys.ToList() }).Wait();

            return CreateFilterResultList("list", launchableItems.Keys.ToList());
        }

        private List<FilterResult> LoadConfigYaml()
        {
            var appdir = System.IO.Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SimpleLauncher");

            if (!System.IO.Directory.Exists(appdir))
            {
                System.IO.Directory.CreateDirectory(appdir);
            }

            var confpath = System.IO.Path.Join(appdir, "simple-launcher.yaml");

            yamlData = YamlLoadUtil.Load(confpath);

            rpc.InvokeAsync<SetListReply>("TestRPC.SetCommandList", new SetListArgs { List = [.. yamlData.CommandList.Keys] }).Wait();

            return CreateFilterResultList("command", yamlData.CommandList.Keys.ToList());
        }

        private async void ExecuteFilter()
        {
            var filteredResult = await rpc.InvokeAsync<FilterReply>("TestRPC.Filter", new FilterArgs { Pattern = _pattern });
            if (filteredResult != null)
            {
                filteredResult.Results.Sort((a, b) =>
                {
                    return b.Score - a.Score;
                });
                FilteredResult = filteredResult.Results;
            }
        }

        private List<FilterResult> CreateFilterResultList(string type, List<string> keys)
        {
            var results = new List<FilterResult>();
            foreach (var key in keys)
            {
                results.Add(new FilterResult { Type= type, Text = key, Score = 100, Pos = new List<int> { -1 } });
            }

            return results;
        }

        private static LaunchableItems GetInstalledApps()
        {
            var apps = new LaunchableItems();

            foreach (var lnk in GetAllShortcuts())
            {
                if (IsValidAppShortcut(lnk, out var name))
                {
                    var launchableItem = new LaunchableItem
                    {
                        DisplayName = name,
                        Path = lnk
                    };
                    apps[launchableItem.DisplayName] = launchableItem;
                }
            }

            return apps;
        }

        static bool IsHiddenByMetadata(RegistryKey key)
        {
            if ((int?)key.GetValue("SystemComponent") == 1)
                return true;

            if (key.GetValue("ReleaseType")?.ToString() == "Update")
                return true;

            if (!string.IsNullOrEmpty(key.GetValue("ParentKeyName") as string))
                return true;

            return false;
        }

        static IEnumerable<string> GetAllShortcuts()
        {
            var roots = new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu)
            };

            foreach (var root in roots)
            {
                if (!Directory.Exists(root)) continue;

                foreach (var file in EnumerateShortcutsSafe(root))
                    yield return file;
            }
        }

        static IEnumerable<string> EnumerateShortcutsSafe(string root)
        {
            var stack = new Stack<string>();
            stack.Push(root);

            while (stack.Count > 0)
            {
                var dir = stack.Pop();

                string[] files = Array.Empty<string>();
                try
                {
                    files = Directory.GetFiles(dir, "*.lnk");
                }
                catch
                {
                    continue;
                }

                foreach (var f in files)
                    yield return f;

                string[] subDirs = Array.Empty<string>();
                try
                {
                    subDirs = Directory.GetDirectories(dir);
                }
                catch
                {
                    continue;
                }

                foreach (var sd in subDirs)
                    stack.Push(sd);
            }
        }

        static bool IsValidAppShortcut(string lnkPath, out string displayName)
        {
            displayName = System.IO.Path.GetFileNameWithoutExtension(lnkPath);

            var shell = new IWshRuntimeLibrary.WshShell();
            var shortcut = (IWshRuntimeLibrary.IWshShortcut)shell.CreateShortcut(lnkPath);

            var target = shortcut.TargetPath;

            if (string.IsNullOrEmpty(target))
                return false;

            // exe or UWP
            if (target.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ||
                target.StartsWith("shell:AppsFolder", StringComparison.OrdinalIgnoreCase))
            {
                // アンインストーラ除外
                if (displayName.Contains("uninstall", StringComparison.OrdinalIgnoreCase))
                    return false;

                return true;
            }

            return false;
        }
    }
}
