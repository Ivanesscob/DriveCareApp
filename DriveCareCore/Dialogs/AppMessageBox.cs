using System.Windows;

namespace DriveCareCore.Dialogs
{
    /// <summary>
    /// Кастомный диалог/>.
    /// </summary>
    public static class AppMessageBox
    {
        public static MessageBoxResult Show(string messageBoxText)
        {
            return Show(messageBoxText, string.Empty);
        }

        public static MessageBoxResult Show(string messageBoxText, string caption)
        {
            return Show(messageBoxText, caption, MessageBoxButton.OK);
        }

        public static MessageBoxResult Show(string messageBoxText, string caption, MessageBoxButton button)
        {
            return Show(messageBoxText, caption, button, MessageBoxImage.None);
        }

        public static MessageBoxResult Show(string messageBoxText, string caption, MessageBoxButton button, MessageBoxImage icon)
        {
            return Show(messageBoxText, caption, button, icon, null);
        }

        /// <summary>
        /// Показать диалог. Если <paramref name="owner"/> не задан, используется главное окно приложения.
        /// </summary>
        public static MessageBoxResult Show(string messageBoxText, string caption, MessageBoxButton button, MessageBoxImage icon, Window owner)
        {
            var dlg = new CustomMessageBox(messageBoxText, caption, button, icon);
            dlg.Owner = owner ?? Application.Current?.MainWindow;
            dlg.ShowDialog();
            return dlg.Result;
        }
    }
}
