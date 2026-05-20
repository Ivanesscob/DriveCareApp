using DriveCare.Services;
using DriveCareCore.Data.BD;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Controls;
using Task = System.Threading.Tasks.Task;

namespace DriveCare
{
    /// <summary>
    /// Общее состояние приложения: навигация и текущий пользователь после входа.
    /// </summary>
    public static class AppState
    {
        public static Frame MainFrame { get; } = new Frame();

        public static Guid CurrentUserId { get; set; }

        public static User CurrentUser { get; set; }

        public static void Navigate(Page page) => MainFrame.Navigate(page);

        public static void SetFrame<T>() where T : Page, new() => MainFrame.Navigate(new T());

        public static List<Role> UserRoles { get; set; }

        /// <summary>Открыть этот диалог на странице сообщений после Navigate.</summary>
        public static Guid? PendingOpenConversationId { get; set; }

        /// <summary>Вход пользователя и сохранение сессии до выхода из профиля.</summary>
        public static void SignInUser(User user)
        {
            if (user == null)
                return;

            CurrentUserId = user.RowId;
            CurrentUser = user;
            UserRoles = AppConnect.model1.UserRoles
                .Where(ur => ur.UserId == user.RowId)
                .Select(ur => ur.Role)
                .ToList();
            UserSessionStore.Save(user.RowId);
        }

        public static bool TryRestoreSession() =>
            TryRestoreSessionAsync().GetAwaiter().GetResult();

        public static async Task<bool> TryRestoreSessionAsync()
        {
            if (!UserSessionStore.TryLoad(out var userId))
                return false;

            try
            {
                var user = await AppConnect.model1.Users
                    .FirstOrDefaultAsync(u => u.RowId == userId)
                    .ConfigureAwait(false);

                if (user == null)
                {
                    UserSessionStore.Clear();
                    return false;
                }

                SignInUser(user);
                return true;
            }
            catch
            {
                UserSessionStore.Clear();
                return false;
            }
        }

        /// <summary>
        /// Сбрасывает сессию пользователя (выход из аккаунта).
        /// </summary>
        public static void SignOut()
        {
            CurrentUserId = Guid.Empty;
            CurrentUser = null;
            UserRoles = null;
            PendingOpenConversationId = null;
            UserSessionStore.Clear();
        }
    }
}
