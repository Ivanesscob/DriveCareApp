using DriveCareCore.Data.BD;
using DriveCarePro;
using DriveCarePro.Pages.Admin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace DriveCarePro.Pages
{
    public partial class ProHomePage : Page
    {
        public ProHomePage()
        {
            InitializeComponent();
            Loaded += (_, __) => ApplyRoleLayout();
        }

        private void ApplyRoleLayout()
        {
            RefreshSubtitle();

            var isAdmin = AppState.IsCurrentEmployeeProAdmin;
            var isService = AppState.IsCurrentEmployeeServiceWorker;
            var isDealership = AppState.IsCurrentEmployeeDealershipHead;
            var isOwner = AppState.IsCurrentEmployeeOwner;
            var canTasks = AppState.CanAccessEmployeeTasks;

            NavAdminBlock.Visibility = isAdmin ? Visibility.Visible : Visibility.Collapsed;
            NavOwnerBlock.Visibility = isOwner ? Visibility.Visible : Visibility.Collapsed;
            EmployeeTasksSection.Visibility = canTasks ? Visibility.Visible : Visibility.Collapsed;

            if (isAdmin)
                LoadAdminStats();

            if (isDealership && !isService)
                TasksSectionTitle.Text = "Задания автосалона";
            else if (isService && !isDealership)
                TasksSectionTitle.Text = "Задания сервиса";
            else if (canTasks)
                TasksSectionTitle.Text = "Мои задания";

            NavRoleHintText.Text = BuildNavRoleHint(isAdmin, isService, isDealership, isOwner);
            DeskHintText.Text = BuildDeskHint(isAdmin, canTasks, isOwner);

            LoadTasksGrid();
        }

        private static string BuildNavRoleHint(bool isAdmin, bool isService, bool isDealership, bool isOwner)
        {
            var parts = new List<string>();
            if (isAdmin) parts.Add("администратор");
            if (isOwner) parts.Add("владелец");
            if (isService) parts.Add("работник сервиса");
            if (isDealership) parts.Add("глава автосалона");
            if (parts.Count == 0)
                return "Разделы справочников доступны всем сотрудникам Pro.";
            return "Ваши роли: " + string.Join(", ", parts) + ".";
        }

        private static string BuildDeskHint(bool isAdmin, bool canTasks, bool isOwner)
        {
            if (isOwner)
                return "Панель разделов: управление сотрудниками вашей организации и справочники. Ниже — задания, если назначена соответствующая роль.";
            if (canTasks)
                return "Панель разделов — справочники и администрирование. Ниже — ваши задания (двойной щелчок или пункт контекстного меню).";
            if (isAdmin)
                return "Ниже — обзор системы и все административные разделы. Справочники — в блоке «Справочники и сервис».";
            return "Панель разделов — автомобили в ремонте и сотрудники. Задания — при роли сервиса или главы автосалона.";
        }

        private void ManageEmployees_Click(object sender, RoutedEventArgs e) =>
            AppState.Navigate(new OwnerEmployeesManagePage());

        private void RolesConstructor_Click(object sender, RoutedEventArgs e) =>
            AppState.Navigate(new OwnerRolesConstructorPage(systemRolesMode: false));

        private void AdminSystemRoles_Click(object sender, RoutedEventArgs e) =>
            AppState.Navigate(new OwnerRolesConstructorPage(systemRolesMode: true));

        private void RepairCars_Click(object sender, RoutedEventArgs e) =>
            AppState.Navigate(new WorkshopRepairCarsPage());

        private void EmployeesInfo_Click(object sender, RoutedEventArgs e) =>
            AppState.Navigate(new WorkshopEmployeesPage());

        private void RefreshTasks_Click(object sender, RoutedEventArgs e) => LoadTasksGrid();

        private void OpenTaskFromMenu_Click(object sender, RoutedEventArgs e) => OpenSelectedTask();

        private void LoadTasksGrid()
        {
            try
            {
                if (!AppState.CanAccessEmployeeTasks)
                {
                    TasksGrid.ItemsSource = null;
                    return;
                }

                var emp = AppState.CurrentEmployee;
                if (emp == null)
                {
                    TasksGrid.ItemsSource = null;
                    return;
                }

                var db = AppConnect.model1;
                var statusLookup = db.Statuses.ToDictionary(s => s.RowId, s => (s.Name ?? string.Empty).Trim());
                var tasks = db.Tasks
                    .Where(t => t.EmployeeId == emp.RowId)
                    .OrderByDescending(t => t.CreatedAt)
                    .Take(150)
                    .ToList();

                TasksGrid.ItemsSource = tasks.Select(t => new EmployeeTaskRowVm
                {
                    TaskId = t.RowId,
                    Title = string.IsNullOrWhiteSpace(t.Title) ? "—" : t.Title.Trim(),
                    StatusDisplay = statusLookup.TryGetValue(t.StatusId, out var sn) ? sn : "—",
                    DeadlineDisplay = t.Deadline.HasValue ? t.Deadline.Value.ToString("dd.MM.yyyy HH:mm") : "—",
                    CreatedDisplay = t.CreatedAt.ToString("dd.MM.yyyy HH:mm"),
                    CompletedDisplay = t.IsCompleted ? "Да" : "Нет"
                }).ToList();
            }
            catch
            {
                TasksGrid.ItemsSource = new List<EmployeeTaskRowVm>();
            }
        }

        private void TasksGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e) => OpenSelectedTask();

        private void OpenSelectedTask()
        {
            if (TasksGrid.SelectedItem is EmployeeTaskRowVm row)
                AppState.Navigate(new EmployeeTaskCardPage(row.TaskId));
        }

        private void RefreshSubtitle()
        {
            var emp = AppState.CurrentEmployee;
            var name = AppState.FormatEmployeeDisplayName(emp);
            var roles = AppState.UserRoles;
            var rolePart = roles != null && roles.Count > 0
                ? string.Join(", ", roles.Where(r => r != null).Select(r => (r.Name ?? string.Empty).Trim()).Where(s => s.Length > 0))
                : string.Empty;
            SubtitleText.Text = string.IsNullOrEmpty(rolePart)
                ? $"{name} · вы вошли в систему"
                : $"{name} · {rolePart}";
        }

        private void LoadAdminStats()
        {
            try
            {
                var db = AppConnect.model1;
                CntUsers.Text = db.Users.Count().ToString("N0");
                CntEmployees.Text = db.Employees.Count().ToString("N0");
                CntCarSales.Text = db.CarSales.Count().ToString("N0");
                CntParts.Text = db.Parts.Count().ToString("N0");
                CntCompanies.Text = db.Companies.Count().ToString("N0");
                CntCars.Text = db.Cars.Count().ToString("N0");
            }
            catch
            {
                CntUsers.Text = CntEmployees.Text = CntCarSales.Text =
                    CntParts.Text = CntCompanies.Text = CntCars.Text = "—";
            }
        }

        private void ModerationCars_Click(object sender, RoutedEventArgs e) =>
            AppState.Navigate(new AdminCarSaleModerationPage());

        private void ModerationParts_Click(object sender, RoutedEventArgs e) =>
            AppState.Navigate(new AdminPartsModerationPage());

        private void AdminOrganizations_Click(object sender, RoutedEventArgs e) =>
            AppState.Navigate(new AdminOrganizationsPage());

        private void AdminTables_Click(object sender, RoutedEventArgs e) =>
            AppState.Navigate(new AdminTableBrowserPage());

        private void AdminBroadcast_Click(object sender, RoutedEventArgs e) =>
            AppState.Navigate(new AdminBroadcastPage());

        private void Profile_Click(object sender, RoutedEventArgs e) =>
            AppState.Navigate(new ProfileProPage());

        public sealed class EmployeeTaskRowVm
        {
            public Guid TaskId { get; set; }
            public string Title { get; set; }
            public string StatusDisplay { get; set; }
            public string DeadlineDisplay { get; set; }
            public string CreatedDisplay { get; set; }
            public string CompletedDisplay { get; set; }
        }
    }
}
