using System.Windows.Input;

namespace SimpleLauncher.Interop
{
    /// <summary>"Ctrl+Alt+O" のような文字列と修飾キー / キーの組を相互変換する。</summary>
    public sealed class HotKeyData
    {
        public HotKeyData(ModifierKeys modifierKeys, Key key)
        {
            ModifierKeys = modifierKeys;
            Key = key;
        }

        public ModifierKeys ModifierKeys { get; }

        public Key Key { get; }

        /// <exception cref="FormatException">書式が不正な場合。</exception>
        public static HotKeyData Parse(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                throw new FormatException("Hotkey string is empty.");
            }

            var modifiers = ModifierKeys.None;
            Key? key = null;

            foreach (var token in text.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                switch (token)
                {
                    case "Shift":
                        modifiers |= ModifierKeys.Shift;
                        break;
                    case "Ctrl":
                    case "Control":
                        modifiers |= ModifierKeys.Control;
                        break;
                    case "Alt":
                        modifiers |= ModifierKeys.Alt;
                        break;
                    case "Win":
                        modifiers |= ModifierKeys.Windows;
                        break;
                    default:
                        if (key != null)
                        {
                            throw new FormatException($"Hotkey string \"{text}\" has more than one key.");
                        }

                        if (!Enum.TryParse<Key>(token, ignoreCase: true, out var parsed) || parsed == Key.None)
                        {
                            throw new FormatException($"Hotkey string \"{text}\" is invalid: unknown key \"{token}\".");
                        }

                        key = parsed;
                        break;
                }
            }

            if (key == null)
            {
                throw new FormatException($"Hotkey string \"{text}\" has no key.");
            }

            return new HotKeyData(modifiers, key.Value);
        }

        public override string ToString()
        {
            var parts = new List<string>();

            if (ModifierKeys.HasFlag(ModifierKeys.Shift))
            {
                parts.Add("Shift");
            }

            if (ModifierKeys.HasFlag(ModifierKeys.Control))
            {
                parts.Add("Ctrl");
            }

            if (ModifierKeys.HasFlag(ModifierKeys.Alt))
            {
                parts.Add("Alt");
            }

            if (ModifierKeys.HasFlag(ModifierKeys.Windows))
            {
                parts.Add("Win");
            }

            parts.Add(Key.ToString());

            return string.Join("+", parts);
        }
    }
}
