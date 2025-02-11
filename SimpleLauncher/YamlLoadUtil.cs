using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YamlDotNet.Serialization;

namespace SimpleLauncher
{
    public class YamlData
    {
        [YamlMember(Alias = "version")]
        public int Version { get; set; } = 0;
        [YamlMember(Alias = "alias")]
        public Dictionary<string, string> Alias { get; set; } = new Dictionary<string, string>();
        [YamlMember(Alias = "list")]
        public List<string> List { get; set; } = new List<string>();

        public Dictionary<string, CommandInfo> CommandList { get; set; } = new Dictionary<string, CommandInfo>();

        public void BuildCommandList(string filename)
        {
            CommandList = new Dictionary<string, CommandInfo>();

            CommandList["--edit"] = new CommandInfo
            {
                Name = "--edit",
                Exec = "explorer",
                Args = string.Format("\"{0}\"", filename),
            };

            CommandList["--show setting"] = new CommandInfo
            {
                Name = "--show setting",
            };

            foreach (var item in List)
            {
                var splitted = item.Split('\n');
                if (splitted.Length >= 2)
                {
                    var args = "";
                    if(splitted.Length >= 3)
                    {
                        args = string.Join("\n", splitted.Skip(2));
                    }

                    CommandList[splitted[0]] = new CommandInfo
                    {
                        Name = splitted[0],
                        Exec = splitted[1],
                        Args = args,
                    };
                }
            }
        }

        public string GetExecFromAlias(string exec)
        {
            if (Alias.ContainsKey(exec))
            {
                return Alias[exec];
            }
            return exec;
        }
    }

    public class CommandInfo
    {
        public string? Name { get; set; }
        public string? Exec { get; set; }
        public string? Args { get; set; }
    }

    public class YamlLoadUtil
    {
        // デシリアライザ設定
        private static readonly IDeserializer _deserializer;

        static YamlLoadUtil()
        {
            // デシリアライザインスタンス作成
            _deserializer = new DeserializerBuilder().IgnoreUnmatchedProperties().Build();
        }

        /// <summary>
        /// YAMLファイルを読み込みオブジェクトを返却する
        /// </summary>
        /// <typeparam name="T">オブジェクト</typeparam>
        /// <param name="filename">YAMLファイル名</param>
        /// <returns>T; オブジェクト</returns>
        public static YamlData Load(string filename)
        {
            if (!File.Exists(filename))
            {
                var asm = System.Reflection.Assembly.GetExecutingAssembly();
                var src = Path.Combine(Path.GetDirectoryName(asm.Location) ?? ".", "launcher.yaml");
                File.Copy(src, filename);
            }

            using (var input = new StreamReader(filename))
            {
                var result = _deserializer.Deserialize<YamlData>(input);
                result.BuildCommandList(filename);

                return result;

            }
        }
    }
}
