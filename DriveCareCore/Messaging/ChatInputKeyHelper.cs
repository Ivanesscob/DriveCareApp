using System;
using System.Windows.Controls;
using System.Windows.Input;

namespace DriveCareCore.Messaging
{
    /// <summary>Enter — отправить, Shift+Enter — новая строка в поле чата.</summary>
    public static class ChatInputKeyHelper
    {
        public static bool IsEnterKey(KeyEventArgs e)
        {
            if (e == null)
                return false;
            if (e.Key == Key.Enter || e.Key == Key.Return)
                return true;
            return e.Key == Key.System && (e.SystemKey == Key.Return || e.SystemKey == Key.Enter);
        }

        public static bool IsShiftHeld => (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;

        public static void InsertNewLine(TextBox textBox)
        {
            if (textBox == null)
                return;

            var text = textBox.Text ?? string.Empty;
            var index = Math.Max(0, Math.Min(textBox.CaretIndex, text.Length));
            const string nl = "\n";
            textBox.Text = text.Insert(index, nl);
            textBox.CaretIndex = index + nl.Length;
        }
    }
}
