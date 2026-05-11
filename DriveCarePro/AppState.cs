using DriveCareCore.Data.BD;
using DriveCarePro.Pages.LoginPages;
using DriveCarePro.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;

namespace DriveCarePro
{
    public static class AppState
    {
        public static Frame MainFrame { get; } = new Frame();

        public static Guid CurrentUserId { get; set; }

        public static Users CurrentUser { get; set; }
        public static Employees CurrentEmployee { get; set; }

        public static void Navigate(Page page) => MainFrame.Navigate(page);

        public static void SetFrame<T>() where T : Page, new() => MainFrame.Navigate(new T());

        public static List<Roles> UserRoles { get; set; }

        /// <summary>Доступ к панели Pro-администратора (роль по имени содержит «админ» или «admin»).</summary>
        public static bool IsCurrentEmployeeProAdmin =>
            UserRoles != null &&
            UserRoles.Any(r => r != null && IsAdminRoleName(r.Name));

        /// <summary>Задания мастерской: сотрудник привязан к записи Workshops (WorkshopId задан).</summary>
        public static bool IsCurrentEmployeeWorkshopWorker
        {
            get
            {
                var e = CurrentEmployee;
                if (e == null)
                    return false;
                return e.WorkshopId.HasValue && e.WorkshopId.Value != Guid.Empty;
            }
        }

        private static bool IsAdminRoleName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return false;
            var n = name.Trim().ToLowerInvariant();
            return n.Contains("админ") || n.Contains("admin");
        }

        public static string FormatEmployeeDisplayName(Employees e)
        {
            if (e == null)
                return "Сотрудник";
            var parts = new[] { e.LastName, e.FirstName, e.MidName }
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s.Trim())
                .ToArray();
            if (parts.Length > 0)
                return string.Join(" ", parts);
            return string.IsNullOrWhiteSpace(e.Login) ? "Сотрудник" : e.Login.Trim();
        }

        public static void SignOutToLogin()
        {
            CurrentUserId = Guid.Empty;
            CurrentUser = null;
            CurrentEmployee = null;
            UserRoles = null;
            ThemeService.Initialize();
            SetFrame<LoginPage>();
        }
    }
}
