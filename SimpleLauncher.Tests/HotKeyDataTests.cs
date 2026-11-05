using System.Windows.Input;
using SimpleLauncher.Interop;

namespace SimpleLauncher.Tests
{
    public class HotKeyDataTests
    {
        [Fact]
        public void Parse_CombinesModifiersAndKey()
        {
            var hotKey = HotKeyData.Parse("Ctrl+Alt+O");

            Assert.Equal(ModifierKeys.Control | ModifierKeys.Alt, hotKey.ModifierKeys);
            Assert.Equal(Key.O, hotKey.Key);
        }

        [Theory]
        [InlineData("Shift+F1", ModifierKeys.Shift, Key.F1)]
        [InlineData("Control+Space", ModifierKeys.Control, Key.Space)]
        [InlineData("Win+Alt+D", ModifierKeys.Windows | ModifierKeys.Alt, Key.D)]
        [InlineData(" Ctrl + k ", ModifierKeys.Control, Key.K)]
        [InlineData("Escape", ModifierKeys.None, Key.Escape)]
        public void Parse_AcceptsVariousForms(string text, ModifierKeys expectedModifiers, Key expectedKey)
        {
            var hotKey = HotKeyData.Parse(text);

            Assert.Equal(expectedModifiers, hotKey.ModifierKeys);
            Assert.Equal(expectedKey, hotKey.Key);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("Ctrl+Alt")]
        [InlineData("Ctrl+NoSuchKey")]
        [InlineData("Ctrl+A+B")]
        public void Parse_RejectsInvalidText(string text)
        {
            Assert.Throws<FormatException>(() => HotKeyData.Parse(text));
        }

        [Fact]
        public void ToString_RoundTripsThroughParse()
        {
            var original = new HotKeyData(ModifierKeys.Shift | ModifierKeys.Control | ModifierKeys.Alt, Key.O);

            var text = original.ToString();
            var parsed = HotKeyData.Parse(text);

            Assert.Equal("Shift+Ctrl+Alt+O", text);
            Assert.Equal(original.ModifierKeys, parsed.ModifierKeys);
            Assert.Equal(original.Key, parsed.Key);
        }
    }
}
