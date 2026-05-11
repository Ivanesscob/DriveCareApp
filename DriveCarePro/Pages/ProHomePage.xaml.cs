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
            Loaded += (_, __) =>
            {
                RefreshSubtitle();
                var isAdmin = AppState.IsCurrentEmployeeProAdmin;
                ModerationButton.Visibility = isAdmin ? Visibility.Visible : Visibility.Collapsed;
                AdminPanelButton.Visibility = isAdmin ? Visibility.Visible : Visibility.Collapsed;
                var workshop = AppState.IsCurrentEmployeeWorkshopWorker;
                WorkshopTasksSection.Visibility = workshop ? Visibility.Visible : Visibility.Collapsed;
                DeskHintText.Text = workshop
                    ? "Ниже — ваши задания мастерской. Дважды щёлкните по строке, чтобы открыть карточку: описание работ, авто, клиент, отчёт и завершение с указанием часов."
                    : "Задания и таблица ниже доступны только сотрудникам, привязанным к мастерской (в карточке сотрудника должна быть указана мастерская).";
                LoadTasksGrid();
            };
        }

        private void LoadTasksGrid()
        {
            try
            {
                if (!AppState.IsCurrentEmployeeWorkshopWorker)
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

        private void TasksGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
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

        private void Moderation_Click(object sender, RoutedEventArgs e) =>
            AppState.Navigate(new ModerationHubPage());

        private void AdminPanel_Click(object sender, RoutedEventArgs e) =>
            AppState.Navigate(new AdminHubPage());

        private void Profile_Click(object sender, RoutedEventArgs e) =>
            AppState.Navigate(new ProfileProPage());

        private void Logout_Click(object sender, RoutedEventArgs e) =>
            AppState.SignOutToLogin();

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
