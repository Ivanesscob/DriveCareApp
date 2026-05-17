using DriveCareCore.Data.BD;
using DriveCarePro.Pages;
using DriveCarePro.Pages.LoginPages;
using DriveCarePro.Services;
using System;
using System.Collections.Generic;
using System.Data.Entity.Core;
using System.Data.SqlClient;
using System.Data.Entity;
using System.Linq;
using System.Windows;
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

        /// <summary>Сообщение об ошибке подключения к БД (для экрана входа).</summary>
        public static string ConnectionErrorMessage { get; private set; }

        /// <summary>Панель платформенного администратора (модерация, справочники, статистика).</summary>
        public static bool CanAccessAdminPanel =>
            HasAnyAssignedPermission(ProPermissions.AdminPanelCodes) ||
            (HasNoAssignedPermissions() && HasAdminRoleByName());

        /// <summary>Рабочая панель сотрудника организации (сервис, владелец, салон).</summary>
        public static bool CanAccessEmployeeWorkspace =>
            HasAnyAssignedPermission(ProPermissions.WorkspaceCodes) ||
            IsCurrentEmployeeOwner ||
            IsCurrentEmployeeServiceWorker ||
            IsCurrentEmployeeDealershipHead ||
            (HasNoAssignedPermissions() && !HasAdminRoleByName());

        /// <summary>Доступ к админ-страницам и действиям платформы.</summary>
        public static bool IsCurrentEmployeeProAdmin => CanAccessAdminPanel;

        public static bool HasPermission(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
                return true;

            if (HasAssignedPermission(code))
                return true;

            if (!HasNoAssignedPermissions())
                return false;

            if (ProPermissions.IsAdminPanelCode(code))
                return HasAdminRoleByName();

            return IsCurrentEmployeeOwner ||
                   IsCurrentEmployeeServiceWorker ||
                   IsCurrentEmployeeDealershipHead;
        }

        public static bool HasAnyPermission(params string[] codes)
        {
            if (codes == null || codes.Length == 0)
                return true;
            return codes.Any(HasPermission);
        }

        public static bool HasAssignedPermission(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
                return false;
            var mine = EmployeePermissionService.CurrentCodes;
            return mine != null && mine.Contains(code.Trim());
        }

        public static bool HasAnyAssignedPermission(params string[] codes)
        {
            if (codes == null || codes.Length == 0)
                return false;
            var mine = EmployeePermissionService.CurrentCodes;
            if (mine == null || mine.Count == 0)
                return false;
            return codes.Any(c => !string.IsNullOrWhiteSpace(c) && mine.Contains(c.Trim()));
        }

        private static bool HasNoAssignedPermissions()
        {
            var mine = EmployeePermissionService.CurrentCodes;
            return mine == null || mine.Count == 0;
        }

        private static bool HasAdminRoleByName() =>
            UserRoles != null && UserRoles.Any(r => r != null && IsAdminRoleName(r.Name));

        public static void SetControlVisible(FrameworkElement element, bool visible)
        {
            if (element == null)
                return;
            element.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        }

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
        public static bool CanManageOrganizationEmployees =>
            CanAccessEmployeeWorkspace &&
            HasAnyPermission(
                ProPermissions.ViewEmployees,
                ProPermissions.EditEmployees,
                ProPermissions.CreateEmployees,
                ProPermissions.DeleteEmployees);

        /// <summary>Таблица и карточки заданий на главной Pro.</summary>
        public static bool CanAccessEmployeeTasks =>
            CanAccessEmployeeWorkspace &&
            HasAnyPermission(ProPermissions.ViewTasks, ProPermissions.EditTasks, ProPermissions.CreateTasks);

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

        /// <summary>Роль владельца организации («владелец», «owner» в названии).</summary>
        public static bool IsOwnerRoleName(string name)
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

        /// <summary>Выбранный сотрудник — текущий пользователь приложения.</summary>
        public static bool IsLoggedInEmployee(Guid employeeId) =>
            employeeId != Guid.Empty &&
            CurrentUserId != Guid.Empty &&
            CurrentUserId == employeeId;

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
        public static void SignInEmployee(Employee employee) =>
            SignInEmployeeAsync(employee).GetAwaiter().GetResult();

        public static async System.Threading.Tasks.Task SignInEmployeeAsync(Employee employee)
        {
            if (employee == null)
                return;

            CurrentUserId = employee.RowId;
            CurrentEmployee = employee;
            UserRoles = await DatabaseExecutor.WithDbAsync(db =>
                db.EmployeeRolesMaps
                    .Where(er => er.EmployeeId == employee.RowId)
                    .Select(er => er.Role)
                    .ToListAsync()).ConfigureAwait(true);

            await EmployeePermissionService.RefreshForEmployeeAsync(employee, UserRoles).ConfigureAwait(true);
            ProSessionStore.Save(employee.RowId);
            ThemeService.LoadForCurrentEmployee();
            ClearConnectionError();
        }

        public static bool TryRestoreSession() =>
            TryRestoreSessionAsync().GetAwaiter().GetResult();

        public static async System.Threading.Tasks.Task<bool> TryRestoreSessionAsync()
        {
            ConnectionErrorMessage = null;

            if (!ProSessionStore.TryLoad(out var employeeId))
                return false;

            try
            {
                var employee = await DatabaseExecutor.WithDbAsync(db =>
                    db.Employees.FirstOrDefaultAsync(e => e.RowId == employeeId)).ConfigureAwait(true);

                if (employee == null)
                {
                    ProSessionStore.Clear();
                    return false;
                }

                await SignInEmployeeAsync(employee).ConfigureAwait(true);
                return true;
            }
            catch (Exception ex) when (IsDatabaseConnectionError(ex))
            {
                ConnectionErrorMessage = BuildConnectionErrorMessage(ex);
                ProSessionStore.Clear();
                return false;
            }
        }

        public static void ClearConnectionError() => ConnectionErrorMessage = null;

        public static string TakeConnectionErrorMessage()
        {
            var msg = ConnectionErrorMessage;
            ConnectionErrorMessage = null;
            return msg;
        }

        public static bool IsDatabaseConnectionError(Exception ex)
        {
            for (var current = ex; current != null; current = current.InnerException)
            {
                if (current is SqlException || current is EntityException)
                    return true;
            }
            return false;
        }

        public static string BuildConnectionErrorMessage(Exception ex)
        {
            var root = ex;
            while (root.InnerException != null)
                root = root.InnerException;

            return "Не удалось подключиться к базе данных DriveCareDB.\n\n" +
                   root.Message +
                   "\n\nПроверьте, что SQL Server запущен и доступен по сети (см. строку подключения в App.config).";
        }

        public static void SignOutToLogin()
        {
            CurrentUserId = Guid.Empty;
            CurrentUser = null;
            CurrentEmployee = null;
            UserRoles = null;
            EmployeePermissionService.Clear();
            ProSessionStore.Clear();
            ThemeService.Initialize();
            SetFrame<LoginPage>();
        }
    }
}
