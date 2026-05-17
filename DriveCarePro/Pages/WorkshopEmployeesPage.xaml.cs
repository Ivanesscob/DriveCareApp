using DriveCareCore.Data.BD;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace DriveCarePro.Pages
{
    public partial class WorkshopEmployeesPage : Page
    {
        public WorkshopEmployeesPage()
        {
            InitializeComponent();
            Loaded += (_, __) => Refresh();
        }

        private void Back_Click(object sender, RoutedEventArgs e) =>
            AppState.Navigate(new ProHomePage());

        private void Refresh_Click(object sender, RoutedEventArgs e) => Refresh();

        private void Refresh()
        {
            try
            {
                var db = AppConnect.model1;
                var workshopNames = db.Workshops.ToDictionary(w => w.RowId, w => (w.Name ?? string.Empty).Trim());
                var roleMaps = db.EmployeeRolesMaps.ToList();
                var roles = db.Roles.ToDictionary(r => r.RowId, r => (r.Name ?? string.Empty).Trim());

                var employees = db.Employees
                    .OrderBy(e => e.LastName).ThenBy(e => e.FirstName)
                    .Take(300)
                    .ToList();

                var list = employees.Select(e =>
                {
                    var roleIds = roleMaps.Where(m => m.EmployeeId == e.RowId).Select(m => m.RoleId).ToList();
                    var roleText = roleIds.Count == 0
                        ? "—"
                        : string.Join(", ", roleIds.Select(id => roles.TryGetValue(id, out var n) ? n : "—").Where(s => s != "—"));
                    if (string.IsNullOrWhiteSpace(roleText))
                        roleText = "—";

                    var ws = e.WorkshopId.HasValue && workshopNames.TryGetValue(e.WorkshopId.Value, out var wn)
                        ? wn : "—";

                    return new EmployeeRowVm
                    {
                        FullName = AppState.FormatEmployeeDisplayName(e),
                        Login = string.IsNullOrWhiteSpace(e.Login) ? "—" : e.Login.Trim(),
                        Email = string.IsNullOrWhiteSpace(e.Email) ? "—" : e.Email.Trim(),
                        Phone = string.IsNullOrWhiteSpace(e.Phone) ? "—" : e.Phone.Trim(),
                        WorkshopName = string.IsNullOrWhiteSpace(ws) ? "—" : ws,
                        RolesDisplay = roleText
                    };
                }).ToList();

                Grid.ItemsSource = list;
                HintText.Text = $"Сотрудников: {list.Count}.";
            }
            catch (Exception ex)
            {
                Grid.ItemsSource = null;
                HintText.Text = "Ошибка загрузки: " + ex.Message;
            }
        }

        private sealed class EmployeeRowVm
        {
            public string FullName { get; set; }
            public string Login { get; set; }
            public string Email { get; set; }
            public string Phone { get; set; }
            public string WorkshopName { get; set; }
            public string RolesDisplay { get; set; }
        }
    }
}
