using DriveCareCore.Data.BD;
using System;
using System.Windows.Controls;

namespace DriveCareCore
{
    /// <summary>
    /// Общее состояние приложения: навигация и текущий пользователь после входа.
    /// </summary>
    public static class AppState
    {
        public static Frame MainFrame { get; } = new Frame();

        public static Guid CurrentUserId { get; set; }

        public static Users CurrentUser { get; set; }

        public static void Navigate(Page page) => MainFrame.Navigate(page);

        public static void SetFrame<T>() where T : Page, new() => MainFrame.Navigate(new T());
    }
}
