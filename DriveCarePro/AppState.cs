using DriveCareCore.Data.BD;
using DriveCarePro.Pages;
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

        public static User CurrentUser { get; set; }
        public static Employee CurrentEmployee { get; set; }

        public static void Navigate(Page page) => MainFrame.Navigate(page);

        public static void SetFrame<T>() where T : Page, new() => MainFrame.Navigate(new T());

        public static List<Role> UserRoles { get; set; }

        /// <summary>Доступ к панели Pro-администратора (роль по имени содержит «админ» или «admin»).</summary>
        public static bool IsCurrentEmployeeProAdmin =>
            UserRoles != null &&
            UserRoles.Any(r => r != null && IsAdminRoleName(r.Name));

        /// <summary>Задания сервиса: роль с «сервис» или «service» в названии.</summary>
        public static bool IsCurrentEmployeeServiceWorker =>
            UserRoles != null && UserRoles.Any(r => r != null && IsServiceRoleName(r.Name));

        /// <summary>Задания автосалона: роль главы автосалона («автосалон», «глава»+«салон», dealership и т.п.).</summary>
        public static bool IsCurrentEmployeeDealershipHead =>
            UserRoles != null && UserRoles.Any(r => r != null && IsDealershipHeadRoleName(r.Name));

        /// <summary>Владелец: роль с «владелец» или «owner» в названии.</summary>
        public static bool IsCurrentEmployeeOwner =>
            UserRoles != null && UserRoles.Any(r => r != null && IsOwnerRoleName(r.Name));

        /// <summary>Управление сотрудниками своей организации.</summary>
        public static bool CanManageOrganizationEmployees => IsCurrentEmployeeOwner;

        /// <summary>Таблица и карточки заданий на главной Pro.</summary>
        public static bool CanAccessEmployeeTasks =>
            IsCurrentEmployeeServiceWorker || IsCurrentEmployeeDealershipHead;

        private static bool IsServiceRoleName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return false;
            var n = name.Trim().ToLowerInvariant();
            return n.Contains("сервис") || n.Contains("service");
        }

        private static bool IsDealershipHeadRoleName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return false;
            var n = name.Trim().ToLowerInvariant();
            if (n.Contains("автосалон") || n.Contains("dealership") || n.Contains("car showroom"))
                return true;
            return n.Contains("глава") && (n.Contains("салон") || n.Contains("авто"));
        }

        private static bool IsOwnerRoleName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return false;
            var n = name.Trim().ToLowerInvariant();
            return n.Contains("владелец") || n.Contains("owner");
        }

        private static bool IsAdminRoleName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return false;
            var n = name.Trim().ToLowerInvariant();
            return n.Contains("админ") || n.Contains("admin");
        }

        public static string FormatEmployeeDisplayName(Employee e)
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

        /// <summary>Вход сотрудника: состояние приложения + сохранение сессии.</summary>
        public static void SignInEmployee(Employee employee)
        {
            if (employee == null)
                return;

            var db = AppConnect.model1;
            CurrentUserId = employee.RowId;
            CurrentEmployee = employee;
            UserRoles = db.EmployeeRolesMaps
                .Where(er => er.EmployeeId == employee.RowId)
                .Select(er => er.Role)
                .ToList();
            ProSessionStore.Save(employee.RowId);
            ThemeService.LoadForCurrentEmployee();
        }

        /// <summary>Восстановить вход после перезапуска приложения.</summary>
        public static bool TryRestoreSession()
        {
            if (!ProSessionStore.TryLoad(out var employeeId))
                return false;

            var employee = AppConnect.model1.Employees.FirstOrDefault(e => e.RowId == employeeId);
            if (employee == null)
            {
                ProSessionStore.Clear();
                return false;
            }

            SignInEmployee(employee);
            return true;
        }

        public static void SignOutToLogin()
        {
            CurrentUserId = Guid.Empty;
            CurrentUser = null;
            CurrentEmployee = null;
            UserRoles = null;
            ProSessionStore.Clear();
            ThemeService.Initialize();
            SetFrame<LoginPage>();
        }
    }
}
