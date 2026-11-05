namespace SimpleLauncher.Services.Fuzzy
{
    /// <summary>fzf の charClass。順序に意味がある (NonWord 以上が「語を構成しうる文字」)。</summary>
    internal enum CharClass
    {
        White = 0,
        NonWord,
        Delimiter,
        Lower,
        Upper,
        Letter,
        Number,
    }

    /// <summary>
    /// fzf の <c>--scheme</c> に相当する、境界ボーナスの重み付け。
    /// <see cref="Default"/> は一般的なテキスト、<see cref="Path"/> はファイルパス向け。
    /// </summary>
    public sealed class FzfScheme
    {
        private const string WhiteChars = " \t\n\v\f\r";

        /// <summary>fzf の "default" スキーム。</summary>
        public static FzfScheme Default { get; } = new(
            bonusBoundaryWhite: FzfMatcher.BonusBoundary + 2,
            bonusBoundaryDelimiter: FzfMatcher.BonusBoundary + 1,
            delimiters: "/,:;|",
            initialCharClass: CharClass.White);

        /// <summary>fzf の "path" スキーム (Windows)。区切りは <c>\</c> と <c>/</c>。</summary>
        public static FzfScheme Path { get; } = new(
            bonusBoundaryWhite: FzfMatcher.BonusBoundary,
            bonusBoundaryDelimiter: FzfMatcher.BonusBoundary + 1,
            delimiters: "\\/",
            initialCharClass: CharClass.Delimiter);

        private readonly string _delimiters;
        private readonly CharClass[] _asciiClasses = new CharClass[128];
        private readonly int[,] _bonusMatrix = new int[(int)CharClass.Number + 1, (int)CharClass.Number + 1];

        private FzfScheme(int bonusBoundaryWhite, int bonusBoundaryDelimiter, string delimiters, CharClass initialCharClass)
        {
            BonusBoundaryWhite = bonusBoundaryWhite;
            BonusBoundaryDelimiter = bonusBoundaryDelimiter;
            _delimiters = delimiters;
            InitialCharClass = initialCharClass;

            for (var i = 0; i < _asciiClasses.Length; i++)
            {
                _asciiClasses[i] = ClassifyAscii((char)i);
            }

            for (var prev = 0; prev <= (int)CharClass.Number; prev++)
            {
                for (var cur = 0; cur <= (int)CharClass.Number; cur++)
                {
                    _bonusMatrix[prev, cur] = BonusFor((CharClass)prev, (CharClass)cur);
                }
            }
        }

        internal int BonusBoundaryWhite { get; }

        internal int BonusBoundaryDelimiter { get; }

        internal CharClass InitialCharClass { get; }

        internal CharClass ClassOf(char c)
        {
            if (c < 128)
            {
                return _asciiClasses[c];
            }

            if (char.IsLower(c)) return CharClass.Lower;
            if (char.IsUpper(c)) return CharClass.Upper;
            if (char.IsNumber(c)) return CharClass.Number;
            if (char.IsLetter(c)) return CharClass.Letter;
            if (char.IsWhiteSpace(c)) return CharClass.White;
            if (_delimiters.Contains(c)) return CharClass.Delimiter;
            return CharClass.NonWord;
        }

        internal int Bonus(CharClass prev, CharClass current) => _bonusMatrix[(int)prev, (int)current];

        private CharClass ClassifyAscii(char c)
        {
            if (c >= 'a' && c <= 'z') return CharClass.Lower;
            if (c >= 'A' && c <= 'Z') return CharClass.Upper;
            if (c >= '0' && c <= '9') return CharClass.Number;
            if (WhiteChars.Contains(c)) return CharClass.White;
            if (_delimiters.Contains(c)) return CharClass.Delimiter;
            return CharClass.NonWord;
        }

        private int BonusFor(CharClass prev, CharClass current)
        {
            if (current >= CharClass.NonWord)
            {
                switch (prev)
                {
                    case CharClass.White:
                        return BonusBoundaryWhite;       // 空白の直後の語頭
                    case CharClass.Delimiter:
                        return BonusBoundaryDelimiter;   // 区切り文字の直後の語頭
                    case CharClass.NonWord:
                        return FzfMatcher.BonusBoundary; // 語境界
                }
            }

            if ((prev == CharClass.Lower && current == CharClass.Upper)
                || (prev != CharClass.Number && current == CharClass.Number))
            {
                return FzfMatcher.BonusCamel123;         // camelCase / letter123
            }

            switch (current)
            {
                case CharClass.NonWord:
                case CharClass.Delimiter:
                    return FzfMatcher.BonusNonWord;
                case CharClass.White:
                    return BonusBoundaryWhite;
                default:
                    return 0;
            }
        }
    }
}
